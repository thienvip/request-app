using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using src.Core.Domains;
using src.Repositories.Request;
using src.Web.Common;
using src.Web.Common.Mvc.Alerts;

namespace src.Web.Controllers
{
    [Authorize]
    public class HistoryController : Controller
    {
        private readonly IUserSession _userSession;
        private readonly IRequestRepository _requestRepository;
        private readonly ILogger<HistoryController> _logger;
        public HistoryController(IUserSession userSession, IRequestRepository requestRepository, ILogger<HistoryController> logger)
        {
            _userSession = userSession;
            _requestRepository = requestRepository;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var changeRequests =  await _requestRepository.GetChangeRequestByUserId(_userSession.Id);        
                return View(changeRequests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return RedirectToAction(nameof(Index)).WithError(ex.Message);
            }
        }



    }
}
