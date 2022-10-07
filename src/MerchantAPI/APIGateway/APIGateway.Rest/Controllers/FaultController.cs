// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Faults;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.APIGateway.Rest.ViewModels.Faults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("test/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  [ServiceFilter(typeof(HttpsRequiredAttribute))]
  public class FaultController : ControllerBase
  {
    readonly IFaultManager faultManager;

    public FaultController(IFaultManager faultManager)
    {
      this.faultManager = faultManager ?? throw new ArgumentNullException(nameof(faultManager));
    }

    /// <summary>
    /// Add a new fault of type DbFault or SimulateSendTxs.
    /// </summary>
    /// <param name="data"></param>
    /// <returns>New fault details.</returns>
    [HttpPost]
    public ActionResult<FaultTriggerViewModelGet> AddFault(FaultTriggerViewModelCreate data)
    {
      var fault = data.ToDomainObject();
      var result = faultManager.GetFaultById(fault.Id);
      if (result != null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Fault '{data.Id}' already exists";
        return Conflict(problemDetail);
      }
      faultManager.Add(fault);

      return CreatedAtAction(nameof(Get),
        new { id = fault.Id },
        new FaultTriggerViewModelGet(fault));
    }

    /// <summary>
    /// Update selected fault information.
    /// </summary>
    /// <param name="id">Id of the selected fault.</param>
    /// <param name="data"></param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public ActionResult Put(string id, FaultTriggerViewModelCreate data)
    {
      data.Id = id;
      var fault = data.ToDomainObject();

      if (faultManager.GetFaultById(fault.Id) == null)
      {
        return NotFound();
      }
      faultManager.Update(fault);

      return NoContent();
    }

    /// <summary>
    /// Get selected fault details.
    /// </summary>
    /// <param name="id">Id of the selected fault.</param>
    /// <returns>Fault details.</returns>
    [HttpGet("{id}")]
    public ActionResult<FaultTriggerViewModelGet> Get(string id)
    {
      var result = faultManager.GetFaultById(id);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new FaultTriggerViewModelGet(result));
    }

    /// <summary>
    /// Get list of all faults.
    /// </summary>
    /// <returns>List of all faults.</returns>
    [HttpGet]
    public ActionResult<List<FaultTriggerViewModelGet>> Get()
    {
      List<FaultTriggerViewModelGet> faults = new();
      faultManager.GetList().ForEach(x => faults.Add(new FaultTriggerViewModelGet(x)));
      return Ok(faults);
    }

    /// <summary>
    /// Remove selected fault.
    /// </summary>
    /// <param name="id">Id of the selected fault.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public IActionResult RemoveFault(string id)
    {
      faultManager.Remove(id);
      return NoContent();
    }

    /// <summary>
    /// Clear all faults.
    /// </summary>
    /// <returns></returns>
    [HttpPost("clearall")]
    public IActionResult ClearFaults()
    {
      faultManager.Clear();
      return NoContent();
    }
  }
}
