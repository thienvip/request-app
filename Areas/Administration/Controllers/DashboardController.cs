
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using src.Core;
using src.Repositories.Request;

namespace MealApplication.Areas.Administration.Controllers
{
    [Area(Constants.Areas.Administration)]
    [Authorize(Policy = Constants.RoleNames.Administrator)]
    [Produces("application/json")]

    public class DashboardController : Controller
    {
        private readonly IRequestRepository _requestRepository;
        public DashboardController(IRequestRepository requestRepository)
        {
            _requestRepository = requestRepository;
        }
        public async Task<IActionResult> Index()
        {
            var requests = await _requestRepository.GetAllChangeRequestsNotMap() ;
            return View(requests);
        }
    }
}
