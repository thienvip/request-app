using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using src.Core;
using src.Core.Domains;
using src.Repositories.Messages;
using src.Repositories.Roles;
using src.Repositories.Users;
using src.Web.Common;
using src.Web.Common.Models.UserViewModels;
using src.Web.Common.Mvc.Alerts;
using System.DirectoryServices.AccountManagement;


namespace src.Web.Areas.Administration.Controllers
{
    [Area(Constants.Areas.Administration)]
    [Authorize(Policy = Constants.RoleNames.Administrator)]
    [Produces("application/json")]
    public class UsersController : Controller
    {
        private readonly IDateTime _dateTime;
        private readonly IMapper _mapper;
        private readonly IMessageService _messageService;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserSession _userSession;

        public UsersController(
            IDateTime dateTime,
            IMapper mapper,
            IMessageService messageService,
            IRoleRepository roleRepository,
            IUserRepository userRepository,
            IUserSession userSession)
        {
            _dateTime = dateTime;
            _mapper = mapper;
            _roleRepository = roleRepository;
            _messageService = messageService;
            _userRepository = userRepository;
            _userSession = userSession;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var entities = await _userRepository.GetUsersAsync();
                var roles = await _roleRepository.GetAllRolesAsync();
                var models = entities.Select(e => _mapper.Map<User, UserViewModel>(e)).ToList();
                foreach (var m in models)
                {
                    if (m.AuthorizedRoleIds.Any())
                    {
                        m.AuthorizedRoleNames = string.Join(",",
                            roles.Where(r => m.AuthorizedRoleIds.Contains(r.Id)).Select(r => r.Name).OrderBy(r => r)
                                .ToArray());
                    }
                }
                return Json(new { data = models });
            }
            catch (Exception ex)
            {
                return Json(new { sucess = false, message = ex.Message });
            }
        }

        public async Task<ActionResult> AddOrEdit(int Id)
        {
            if (Id == 0)
            {
                UserCreateUpdateViewModel viewmodel = new UserCreateUpdateViewModel();
                viewmodel.Id = 0;
                viewmodel.CreatedBy = _userSession.UserName;
                viewmodel.AvailableRoles = await GetAvailableRoles();
                return View(viewmodel);
            }
            else
            {
                var entity = await _userRepository.GetUserByIdAsync(Id);
                var viewmodel = _mapper.Map<User, UserCreateUpdateViewModel>(entity);
                viewmodel.SelectedRoleIds = (await _roleRepository.GetUserRolesForUserAsync(entity.Id)).Select(r => r.RoleId).ToList();
                viewmodel.AvailableRoles = await GetAvailableRoles();
                return View(viewmodel);
            }
        }
        private async Task<IList<SelectListItem>> GetAvailableRoles()
        {
            return (await _roleRepository.GetAllRolesAsync())
                .Select(role => new SelectListItem { Text = role.Name, Value = role.Id.ToString() })
                .ToList();
        }

        [HttpGet("getFirstLastNameFromAD")]
        public IActionResult getFirstLastNameFromAD(string userName)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, "vais.local"))
                {
                    var user = UserPrincipal.FindByIdentity(context, userName);
                    if (user != null)
                    {
                        var userOnDb = _userRepository.GetUserByUserNameAsync(userName);
                        if (userOnDb == null)
                        {
                            return Json(data: $"The UserName {userName} has created !!!");
                        }
                        else
                            return Json(new { success = true, FristName = user.GivenName, LastName = user.Surname });
                    }
                    else
                    {
                        return Json(new { success = false });
                    }
                }
            }
            catch (Exception ex)
            {

                return Json(new { success = 0, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateOrUpdateUser(UserCreateUpdateViewModel model)
        {
            // Check if the model state is valid (checks for validation attributes on the model)
            if (ModelState.IsValid)
            {
                // Logic to handle the form data, for instance, save to database, process, or manipulate the model
                // For this example, let's just return the data as JSON
                return Json(model);
            }

            // If the model state is not valid, return the view with the model to display validation errors
            return View(model);
        }

        public async Task<IActionResult> PostUsers(UserCreateUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                if (model.Id == 0)
                {
                    var dateTimeNow = _dateTime.Now;
                    var username = _userSession.UserName;
                    var user = _mapper.Map<UserCreateUpdateViewModel, User>(model);
                    user.LastLoginDate = dateTimeNow;
                    user.CreatedBy = username;
                    user.CreatedOn = dateTimeNow;
                    user.ModifiedBy = username;
                    user.ModifiedOn = dateTimeNow;

                    var allRoles = await _roleRepository.GetAllRolesAsync();
                    foreach (var role in allRoles)
                    {
                        if (model.SelectedRoleIds.Any(r => r == role.Id))
                            user.UserRoles.Add(new UserRole { User = user, Role = role });
                    }

                    await _userRepository.AddUserAsync(user);
                    return Json(new { success = true, message = $"Add username {model.UserName} successful !" });
                }
                else
                {
                    var user = await _userRepository.GetUserByIdAsync(model.Id);
                    if (user == null)
                    {
                        return RedirectToAction("Index").WithError("Please select a user.");
                    }
                    var dateTimeNow = _dateTime.Now;
                    var username = _userSession.UserName;
                    user = _mapper.Map(model, user);
                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    user.IsActive = model.IsActive;
                    user.LastLoginDate = dateTimeNow;
                    user.ModifiedOn = dateTimeNow;
                    user.ModifiedBy = username;

                    var allRoles = await _roleRepository.GetAllRolesAsync();
                    var allUserRoles = await _roleRepository.GetUserRolesForUserAsync(model.Id);
                    foreach (var role in allRoles)
                    {
                        if (model.SelectedRoleIds.Any(r => r == role.Id))
                        {
                            if (allUserRoles.All(r => r.RoleId != role.Id))
                                user.UserRoles.Add(new UserRole { User = user, Role = role });
                        }
                        else if (allUserRoles.Any(r => r.RoleId == role.Id))
                        {
                            var removingUserRole = allUserRoles.FirstOrDefault(r => r.RoleId == role.Id);
                            user.UserRoles.Remove(removingUserRole);
                        }
                    }
                    await _userRepository.UpdateUserAsync(user);
                    return Json(new { success = true, message = $"Update data infor {model.UserName} successful !" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(id);
                if (user != null)
                {
                    await _userRepository.DeleteUserAsync(user);
                    return Json(new { success = true, message = $"Update data infor {user.UserName} successful !" });
                }
                else
                {
                    return RedirectToAction("Index").WithError("User does not exist.");
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


    }
}
