using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/making-charge-methods")]
[Authorize]
public class MakingChargeMethodsController : ControllerBase
{
    private readonly IAttributesService _attributesService;

    public MakingChargeMethodsController(IAttributesService attributesService)
    {
        _attributesService = attributesService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AttributesView)]
    public ActionResult<List<MakingChargeMethodDto>> GetMakingChargeMethods()
    {
        var list = _attributesService.GetMakingChargeMethods();
        return Ok(list);
    }
}
