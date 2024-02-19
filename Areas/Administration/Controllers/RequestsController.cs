using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using src.Core;
using src.Core.Domains;
using src.Emails;
using src.Localization.Resources;
using src.Repositories.Comments;
using src.Repositories.EmailConfigs;
using src.Repositories.Request;
using src.Repositories.Users;
using src.Web.Common;
using src.Web.Common.Models.RequestModels;
using src.Web.Common.Mvc.Alerts;
using src.Web.Extensions;

namespace src.Web.Areas.Administration.Controllers
{
    [Area(Constants.Areas.Administration)]
    [Authorize(Policy = Constants.RoleNames.Administrator)]
    [Produces("application/json")]
    public class RequestsController : Controller
    {   
        private readonly IRequestRepository _requestRepository;
        public  readonly IUserRepository _userRepository;
        private readonly ILogger<RequestsController> _logger;
        private readonly ICommentsRepository _commentsRepository;
        private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
        private readonly IUserSession _userSession;
        private readonly IBackgroundJobClient _backgroungJobClient;
        private readonly IEmailConfigRepository _emailConfigRepository;
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _cache;

        public RequestsController(ILogger<RequestsController> logger,IRequestRepository requestRepository, IUserRepository userRepository, ICommentsRepository commentsRepository, IStringLocalizer<SharedResource> sharedLocalizer, IUserSession userSession, IBackgroundJobClient backgroungJobClient, IMemoryCache cache, IEmailConfigRepository emailConfigRepository, IEmailSender emailSender)
        {
            _logger = logger;
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _commentsRepository = commentsRepository;
            _sharedLocalizer = sharedLocalizer;
            _userSession = userSession;
            _backgroungJobClient = backgroungJobClient;
            _cache = cache; 
            _emailConfigRepository = emailConfigRepository;
            _emailSender = emailSender;
        }

        public IActionResult Index()
        {
            try
            {
                return View();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return RedirectToAction("Login", "Account").WithError(_sharedLocalizer["message_something_went_wrong"]);
            }
        }

        public async Task<IActionResult> GetRequestChanges()
        {
            try
            {
                var lstRequests = await _requestRepository.GetAllChangeRequests();

                var filteredRequests = lstRequests.Where(
                     x => x.ChangeRequestFlow?.State == "Submitted" && _userSession.IsInRole(Constants.RoleNames.Supporter) ||
                          x.ChangeRequestFlow?.State == "Supported" && _userSession.IsInRole(Constants.RoleNames.BO) && x.ChangeRequestFlow?.Status == false ||
                          x.ChangeRequestFlow?.State == "Approved" && _userSession.IsInRole(Constants.RoleNames.IT) && x.ChangeRequestFlow?.Status == false ||
                          x.ChangeRequestFlow?.State == "Assessed" && _userSession.IsInRole(Constants.RoleNames.Administrator) && x.ChangeRequestFlow?.Status == false
                 ).OrderByDescending(s => s.CreatedDate);

                var responseData = filteredRequests
                .Select(request => new
                {
                    Id = request.Id,
                    Requester = request.Requester,
                    Campus = request.Campus,
                    Application = request.Applications?.Name != null ? request.Applications.Name : "",
                    ApplicationOtherName = request.ApplicationOtherName,
                    RequestType = request.ChangeRequestTypes?.Name != null ? request.ChangeRequestTypes.Name : "",
                    RequestTypeOtherName = request.ChangeRequestOtherName,
                    Priority = request.Prioritys.Name,
                    CreatedDate = request.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    UpdatedUser = request.ChangeRequestFlow.User.UserName,
                    State = request.ChangeRequestFlow.State,
                    Status = request.ChangeRequestFlow.Status
                });
                return Json(new { data = responseData });

            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return Json(new { sucess = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> GetCompletedRequestChanges()
        {
            try
            {
                var lstRequests = await _requestRepository.GetAllChangeRequests();

                var filteredCompletedRequests = lstRequests
                                                .Where(x => x.ChangeRequestFlow.ChangeRequestUserUpdate
                                                .Any(y => y.UserId == _userSession.Id)).ToList();

                var responseData = filteredCompletedRequests
                .Select(request => new
                {
                    Id = request.Id,
                    Requester = request.Requester,
                    Campus = request.Campus,
                    Application = request.Applications?.Name != null ? request.Applications.Name : "",
                    ApplicationOtherName = request.ApplicationOtherName,
                    RequestType = request.ChangeRequestTypes?.Name != null ? request.ChangeRequestTypes.Name : "",
                    RequestTypeOtherName = request.ChangeRequestOtherName,
                    Priority = request.Prioritys.Name,
                    CreatedDate = request.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    UpdatedUser = request.ChangeRequestFlow.User.UserName,
                    State = request.ChangeRequestFlow.State,
                    Status = request.ChangeRequestFlow.Status
                });


                return Json(new { data = responseData });
            }
            catch (Exception  ex)
            {
                return Json(new { sucess = false,message = ex.Message }); ;
            }
        }


        public IActionResult RequestsTracker()
        {
            try
            {             
                return View();         
            }catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return RedirectToAction(nameof(Index)).WithError(_sharedLocalizer["message_something_went_wrong"]);
            }
        }

        public async Task<IActionResult> GetRequestsTracker()
        {
            try
            {
                var lstRequests = new List<ChangeRequest>();
                var requests = await _requestRepository.GetAllChangeRequests();
                if (requests.Count() > 0)
                {
                    lstRequests.AddRange(requests);
                }

                var responseData = lstRequests                  
                .Select(request => new
                {
                    Id = request.Id,
                    Requester = request.Requester,
                    Campus = request.Campus,
                    Application = request.Applications?.Name != null ? request.Applications.Name : "",
                    ApplicationOtherName = request.ApplicationOtherName,
                    RequestType = request.ChangeRequestTypes?.Name != null ? request.ChangeRequestTypes.Name : "",
                    RequestTypeOtherName = request.ChangeRequestOtherName,
                    Priority = request.Prioritys.Name,
                    CreatedDate = request.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    UpdatedUser = request.ChangeRequestFlow.User.UserName,
                    State = request.ChangeRequestFlow.State,
                    Status = request.ChangeRequestFlow.Status
                });
                return Json(new { data = responseData });
            }
            catch (Exception ex)
            {
                return Json(new { sucess = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Detail(Guid id, string param)
        {
           try
           {
                var changeRequest = await _requestRepository.GetChangeRequestById(id) ;           
                var listUsers =  await _userRepository.GetUsersAsync();

                var userInCharge = _userSession.IsInRole(Constants.RoleNames.Supporter)
                                    ? listUsers.Where(user => user.UserRoles?.FirstOrDefault()?.RoleId == 4).ToList()
                                    : _userSession.IsInRole(Constants.RoleNames.BO)
                                    ? listUsers.Where(user => user.UserRoles?.FirstOrDefault()?.RoleId == 5).ToList()
                                    : _userSession.IsInRole(Constants.RoleNames.IT) 
                                    ? listUsers.Where(user => user.UserRoles?.FirstOrDefault()?.RoleId == 1).ToList()
                                    : new List<User>();

                var detailChangeRequest = new ListRequestViewModel
                {
                    ChangeRequestDetail = changeRequest,
                    ListUsers = userInCharge,                  
                };

                ViewData["AdditionalParameter"] = param;
                return View(detailChangeRequest);
           }
           catch
           {
               return RedirectToAction(nameof(Index)).WithError(_sharedLocalizer["message_something_went_wrong"]);
           }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitModal([FromForm] ListRequestViewModel model,bool deny)
        {
            try
            {         
                string host = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}";
                var user = _cache.Get("user") as User;

                model.ChangeRequestDetail.ChangeRequestFlow.Status = deny;
                model.ChangeRequestDetail.ChangeRequestFlow.UpdatedDate = DateTime.Now;
                model.ChangeRequestDetail.ChangeRequestFlow.UpdatedUserId = _userSession.Id;

                if (!string.IsNullOrEmpty(model.ChangeRequestDetail.ChangeRequestFlow.Comment))
                {
                    if (_userSession.IsInRole(Constants.RoleNames.Supporter))
                    {
                        model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 2;
                    }
                    if (_userSession.IsInRole(Constants.RoleNames.BO))
                    {
                        model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 3;
                    }
                    if (_userSession.IsInRole(Constants.RoleNames.IT))
                    {
                        model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 4;
                    }
                    if (_userSession.IsInRole(Constants.RoleNames.Administrator))
                    {
                        model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 5;
                    }


                    var newComment = new Comments
                    {
                        ForeignId = model.ChangeRequestDetail.ChangeRequestFlow.Id,
                        Comment = model.ChangeRequestDetail.ChangeRequestFlow.Comment,
                        UserId = _userSession.Id,
                        CreatedDate = DateTime.Now,
                        ChangeRequestId = model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId,
                    };

                    await _commentsRepository.AddCommentAsync(newComment);
                }

                if (_userSession.IsInRole(Constants.RoleNames.Supporter))
                {
                    model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 2;
                    model.ChangeRequestDetail.ChangeRequestFlow.State = "Supported";
                    model.ChangeRequestDetail.ChangeRequestFlow.NextState = "Approved";

                }
                if (_userSession.IsInRole(Constants.RoleNames.BO))
                {
                    model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 3;
                    model.ChangeRequestDetail.ChangeRequestFlow.State = "Approved";
                    model.ChangeRequestDetail.ChangeRequestFlow.NextState = "Assessed";

                }
                if (_userSession.IsInRole(Constants.RoleNames.IT))
                {
                    model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 4;
                    model.ChangeRequestDetail.ChangeRequestFlow.State = "Assessed";
                    model.ChangeRequestDetail.ChangeRequestFlow.NextState = "Completed";
                }
                if (_userSession.IsInRole(Constants.RoleNames.Administrator))
                {
                    model.ChangeRequestDetail.ChangeRequestFlow.ChangeRequestId = 5;
                    model.ChangeRequestDetail.ChangeRequestFlow.State = "Completed";
                    model.ChangeRequestDetail.ChangeRequestFlow.NextState = "";                  
                    model.ChangeRequestDetail.ChangeRequestFlow.NextStateUserInCharge = _userSession.Id;
                }

                var ChangeRequestUserUpdate = new ChangeRequestUserUpdate()
                {
                    RequestId = model.ChangeRequestDetail.ChangeRequestFlow.Id,
                    UserId = _userSession.Id,
                };

                try
                {
                    await _requestRepository.InsertChangeRequestUserUpdate(ChangeRequestUserUpdate);
                    await _requestRepository.UpdateRequestFlow(model.ChangeRequestDetail.ChangeRequestFlow);              
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                    return RedirectToAction(nameof(Index)).WithError("Request for Change update failed");
                }

                if (user != null)
                {
                    _backgroungJobClient.Enqueue(() => SendAnnounceEmailToUser(user, model.ChangeRequestDetail.ChangeRequestFlow.Id, model.ChangeRequestDetail.ChangeRequestFlow.NextStateUserInCharge, host));

                    if (!_userSession.IsInRole(Constants.RoleNames.Administrator) && deny != true)
                    {
                        _backgroungJobClient.Enqueue(() => SendAnnounceEmailToUserInCharge(user, model.ChangeRequestDetail.ChangeRequestFlow.Id, model.ChangeRequestDetail.ChangeRequestFlow.NextStateUserInCharge, host));
                    }
                }

                return RedirectToAction(nameof(Index)).WithSuccess("Request For Change updated successfully");
                    
            }
            catch
            {
                return RedirectToAction(nameof(Index)).WithError(_sharedLocalizer["message_something_went_wrong"]);
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

        public async Task SendAnnounceEmailToUser(User user, Guid id,int nextStateUserId, string currentUrl)
        {
            var userInCharge = await _userRepository.GetUserByIdAsync(nextStateUserId);
            if (!string.IsNullOrEmpty(user.UserName) && !string.IsNullOrEmpty(userInCharge.UserName))
            {
                var changeRequests = await _requestRepository.GetChangeRequestById(id);
                ChangeRequest changeRequest = changeRequests;
                var emailModel = await _emailConfigRepository.getAllConfig();
                string template = string.Empty;
                template = emailModel.confirmation_request_user.Body;
                template = MessageExtension.ReplaceForUserConfirmation(user, changeRequest, template, currentUrl);

                await _emailSender.SendEmail("qvthien6@gmail.com", emailModel.confirmation_request_user.Subject, template, null, null);
                // await _emailSender.SendEmail(parent.Email, subject, template, null, ccEmail);               
            }
        }

    }
}

