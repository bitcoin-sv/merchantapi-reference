// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Rest.Swagger;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using MerchantAPI.Common;
using MerchantAPI.Common.Extensions;

namespace MerchantAPI.PaymentAggregator.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  public class ServiceLevelController : ControllerBase
  {
    private readonly ILogger<ServiceLevelController> logger;
    private readonly IServiceLevelRepository serviceLevelRepository;

    public ServiceLevelController(
      ILogger<ServiceLevelController> logger,
      IServiceLevelRepository feeQuoteRepository
      )
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.serviceLevelRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
    }

    /// <summary>
    /// Get active service levels.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<ServiceLevelArrayViewModelGet> Get()
    {
      var result = serviceLevelRepository.GetServiceLevels();
      if (!result.Any())
      {
        return NotFound();
      }
      return Ok(new ServiceLevelArrayViewModelGet(result.ToArray()));
    }

    /// <summary>
    /// Create new service level.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<ServiceLevelArrayViewModelGet>> Post([FromBody] ServiceLevelArrayViewModelCreate data)
    {
      logger.LogDebug($"Create new ServiceLevel from data: {data} .");
      var domainModel = data.ToDomainObject();
      var br = this.ReturnBadRequestIfInvalid(domainModel);
      if (br != null)
      {
        return br;
      }

      var newServiceLevels = await serviceLevelRepository.InsertServiceLevelsAsync(domainModel.ServiceLevels);
      if (newServiceLevels == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = $"ServiceLevels not created. Check errors.";
        return BadRequest(problemDetail);
      }

      var returnResult = new ServiceLevelArrayViewModelGet(newServiceLevels);

      return CreatedAtAction(nameof(Get), null, returnResult);
    }
  }
}
