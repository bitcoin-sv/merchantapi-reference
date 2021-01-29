// Copyright (c) 2020 Bitcoin Association

using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using System.Net;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Rest.Swagger;
using Microsoft.AspNetCore.Authorization;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common;
using MerchantAPI.Common.Extensions;

namespace MerchantAPI.PaymentAggregator.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  public class GatewayController : ControllerBase
  {

    private readonly ILogger<GatewayController> logger;
    private readonly IGateways gateways;
    private readonly IClock clock;


    public GatewayController(
      ILogger<GatewayController> logger,
      IGateways gateways,
      IClock clock
      )
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Register a new gateway with merchant api.
    /// </summary>
    /// <param name="data"></param>
    /// <returns>New gateway details.</returns>
    [HttpPost]
    public async Task<ActionResult<GatewayViewModelGet>> Post(GatewayViewModelCreate data)
    {
      logger.LogDebug($"Create new Gateway from data: {data} .");

      var domainModel = data.ToDomainObject(clock.UtcNow());
      var br = this.ReturnBadRequestIfInvalid(domainModel);
      if (br != null)
      {
        return br;
      }

      var created = await gateways.CreateGatewayAsync(domainModel);
      if (created == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.Conflict);
        problemDetail.Title = $"Gateway with url '{data.Url}' already exists";
        return Conflict(problemDetail);
      }

      return CreatedAtAction(nameof(Get),
        new { id = created.Id },
        new GatewayViewModelGet(created));
    }

    /// <summary>
    /// Update selected gateway information.
    /// </summary>
    /// <param name="id">Id of the selected gateway.</param>
    /// <param name="data"></param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public async Task<ActionResult> Put(int id, GatewayViewModelCreate data)
    {
      var domainModel = data.ToDomainObject();
      domainModel.Id = id;
      var br = this.ReturnBadRequestIfInvalid(domainModel);
      if (br != null)
      {
        return br;
      }

      if (!await gateways.UpdateGatewayAsync(domainModel))
      {
        return NotFound();
      }

      return NoContent();
    }

    /// <summary>
    /// Delete selected gateway.
    /// </summary>
    /// <param name="id">Id of the selected gateway.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public IActionResult DeleteGateway(int id)
    {
      gateways.DeleteGateway(id);
      return NoContent();
    }

    /// <summary>
    /// Get selected gateway details.
    /// </summary>
    /// <param name="id">Id of the selected gateway.</param>
    /// <returns>Node details.</returns>
    [HttpGet("{id}")]
    public ActionResult<GatewayViewModelGet> Get(int id)
    {
      var result = gateways.GetGateway(id);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new GatewayViewModelGet(result));
    }

    /// <summary>
    /// Get list of all gateways.
    /// </summary>
    /// <returns>List of gateways.</returns>
    [HttpGet]
    public ActionResult<IEnumerable<GatewayViewModelGet>> Get([FromQuery] bool onlyActive)
    {
      var result = gateways.GetGateways(onlyActive);
      return Ok(result.Select(x => new GatewayViewModelGet(x)));
    }
  }
}