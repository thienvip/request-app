
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using MimeKit;
using src.Core.Domains;
using src.Emails;
using src.Localization.Resources;
using src.Repositories.Applications;
using src.Repositories.EmailConfigs;
using src.Repositories.Request;
using src.Repositories.RequestAttachments;
using src.Repositories.Users;
using src.Web.Common.Models.RequestModels;
using src.Web.Common.Mvc.Alerts;
using src.Web.Extensions;
using Hangfire;
using src.Core;
using src.Web.Common.Validation;
using FluentValidation.Results;

namespace src.Web.Controllers
{
    public class RequestController : Controller
    {
        private readonly ILogger<RequestController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
        private readonly IUserRepository _userRepository;
        private readonly IApplicationRepository _applicationRepository;
        private readonly IRequestRepository _requestRepository;
        private readonly IConfiguration Configuration;
        private readonly IRequestAttachmentsRepository _requestAttachmentsRepository;
        private IWebHostEnvironment _hostingEnvironment;
        private readonly IBackgroundJobClient _backgroungJobClient;
        private readonly IEmailConfigRepository _emailConfigRepository;
        private readonly IEmailSender _emailSender;

        public RequestController(ILogger<RequestController> logger,IMemoryCache cache, IStringLocalizer<SharedResource> sharedLocalizer, IUserRepository userRepository, IApplicationRepository applicationRepository, IRequestRepository requestRepository, 
            IConfiguration configuration, IRequestAttachmentsRepository requestAttachmentsRepository, IWebHostEnvironment hostingEnvironment, IBackgroundJobClient backgroungJobClient, IEmailConfigRepository emailConfigRepository, IEmailSender emailSender)
        {
            _logger = logger;
            _cache = cache;
            _sharedLocalizer = sharedLocalizer;
            _userRepository = userRepository;
            _applicationRepository = applicationRepository;
            _requestRepository = requestRepository;
            Configuration = configuration;
            _requestAttachmentsRepository = requestAttachmentsRepository;
            _hostingEnvironment = hostingEnvironment;
            _backgroungJobClient = backgroungJobClient;
            _emailConfigRepository = emailConfigRepository;
            _emailSender = emailSender;
        }
        public async Task<IActionResult> Index()
        {
            try {
                var user = _cache.Get("user") as User;
                var listUsers = await _userRepository.GetUsersAsync();
                var lstRequest = await _requestRepository.GetChangeRequestByUserId(user.Id);
                var supportedUser = listUsers.Where(user => user.UserRoles?.FirstOrDefault()?.RoleId == 3).ToList();
                _cache.Set("supportedUser", supportedUser);

                var lstApps =  _applicationRepository.GetAllApplications();  
                if (user == null)
                    return RedirectToAction("Login", "Account")
                        .WithError(_sharedLocalizer["message_something_went_wrong"]);
                var requestViewModel = new RequestViewModel()
                {
                    User = user,
                    ChangeRequest = new ChangeRequest { },
                    ChangeRequestFlow = new ChangeRequestFlow(),
                    ListUsers = supportedUser,
                    ListApplications = lstApps,
                    ListChangeRequests = lstRequest,  
                };

                return View(requestViewModel);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return RedirectToAction("Login", "Account").WithError(_sharedLocalizer["message_something_went_wrong"]);
            }       
        }



        [HttpPost]
        public async Task<IActionResult> Submit([FromForm] RequestViewModel data, List<IFormFile> files)
        {

            ChangeRequestValidator validator = new ChangeRequestValidator();
            string errorMessage = string.Empty;

            ValidationResult results = validator.Validate(data.ChangeRequest);

            if (!results.IsValid)
            {
                foreach (var error in results.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    errorMessage += error + "-";
                }

                return RedirectToAction(nameof(Index)).WithError(errorMessage);
            }
            else
            {
                try
                {             
                    var user = _cache.Get("user") as User;
                    var supportedUser = _cache.Get("supportedUser") as List<User>;

                    if(supportedUser != null)
                    {
                        if (!supportedUser.Any(user => user.UserName == data.ChangeRequest.Users.UserName))
                        {
                            return RedirectToAction(nameof(Index)).WithError("No matching supporter found.");
                        }
                    }

                    var userInChargeId = supportedUser.FirstOrDefault(user => user.UserName == data.ChangeRequest.Users.UserName).Id;

                    var requestData = data.ChangeRequest;
                    string host = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}";

                    requestData.CreatedDate = DateTime.Now;
                    requestData.CreatedUserId = user.Id;
                    requestData.Users = null ;

                    var newChangeRequestFlow = new ChangeRequestFlow()
                    {
                        Id = requestData.Id,
                        State = "Submitted",
                        UpdatedUserId = user.Id,
                        UpdatedDate = DateTime.Now,
                        NextState = "Supported",
                        NextStateUserInCharge = userInChargeId,
                        ChangeRequestId = 1,
                        Status = false
                    };

                    foreach (var file in files)
                    {
                        if (file.Length > 0)
                        {
                            var _fileName = Path.GetFileName(file.FileName);
                            var _filePath = Path.Combine(_hostingEnvironment.WebRootPath, "RequestAttachments", _fileName);

                            var filePathView = Path.Combine("/RequestAttachments", _fileName);
                            using (var stream = new FileStream(_filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            var fileUpload = new ChangeRequestAttachment
                            {
                                ChangeRequestId = requestData.Id,
                                FileName = _fileName,
                                FilePath = filePathView
                            };

                            await _requestAttachmentsRepository.InsertRequestAttachments(fileUpload);
                        }
                    }

                    try
                    {
                        await _requestRepository.InsertRequest(requestData);
                        await _requestRepository.InsertRequestFlow(newChangeRequestFlow);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, ex.Message);
                        return RedirectToAction(nameof(Index)).WithError(ex.Message);
                    }

                    if (user != null)
                    {
                        _backgroungJobClient.Enqueue(() => SendAnnounceEmailToUser(user, newChangeRequestFlow.NextStateUserInCharge, host));
                        _backgroungJobClient.Enqueue(() => SendAnnounceEmailToUserInCharge(user, requestData.Id, newChangeRequestFlow.NextStateUserInCharge, host));
                    }
                    return RedirectToAction(nameof(Success));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                    return RedirectToAction(nameof(AccountController.Login), "Account")
                        .WithError(_sharedLocalizer["message_something_went_wrong"]);
                }
            }
               
        }

        public async Task<IActionResult> Success()
        {
            try
            {
                var user = _cache.Get("user") as User;
                var lstUsers = _cache.Get("supportedUser") as List<User>;
                var lstRequest = await _requestRepository.GetChangeRequestByUserId(user.Id);                       
                var listRequestViewModel = new ListRequestViewModel()
                {                
                    ChangeRequest = lstRequest,                
                    ListUsers = lstUsers
                };
                return View(listRequestViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return RedirectToAction("Login", "Account").WithError(_sharedLocalizer["message_something_went_wrong"]);
            }
        }

        public async Task<IActionResult> DetailModal(Guid id)
        {
            try
            {
                var changeRequest = await _requestRepository.GetChangeRequestById(id);             
                return View(changeRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return RedirectToAction(nameof(Index)).WithError(ex.Message);
            }
        }


        public async Task SendAnnounceEmailToUser(User user,int nextStateUserId, string currentUrl)
        {
            var userInCharge = await _userRepository.GetUserByIdAsync(nextStateUserId);
            if (!string.IsNullOrEmpty(user.UserName) && !string.IsNullOrEmpty(userInCharge.UserName))
            {
                var changeRequests = await _requestRepository.GetChangeRequestByUserId(user.Id);
                ChangeRequest changeRequest = changeRequests.FirstOrDefault() ?? new ChangeRequest() ;
                var emailModel = await _emailConfigRepository.getAllConfig();             
                string template = string.Empty;
                template = emailModel.confirmation_request_user.Body;
                template = MessageExtension.ReplaceForUserConfirmation(user, changeRequest, template, currentUrl);            
                    
                await _emailSender.SendEmail("qvthien6@gmail.com", emailModel.confirmation_request_user.Subject, template, null, null);
               // await _emailSender.SendEmail(parent.Email, subject, template, null, ccEmail);               
            }
        }
        public async Task SendAnnounceEmailToUserInCharge(User user, Guid id, int nextStateUserId, string currentUrl)
        {
            var userInCharge = await _userRepository.GetUserByIdAsync(nextStateUserId);
            if (!string.IsNullOrEmpty(user.UserName) && !string.IsNullOrEmpty(userInCharge.UserName))
            {
                var emailModel = await _emailConfigRepository.getAllConfig();
                string template = string.Empty;
                template = emailModel.confirmation_user_in_charge.Body;
                template = MessageExtension.ReplaceForUserInCharger(user, userInCharge, id, template, currentUrl);
                await _emailSender.SendEmail("qvthien6@gmail.com", emailModel.confirmation_user_in_charge.Subject, template, null, null);
                // await _emailSender.SendEmail(parent.Email, subject, template, null, ccEmail);               
            }
        }

    }
}
