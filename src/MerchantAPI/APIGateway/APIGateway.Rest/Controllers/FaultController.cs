// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Faults;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.APIGateway.Rest.ViewModels.Faults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

    [HttpGet]
    public Task<List<FaultTriggerViewModelGet>> GetFaultTriggers()
    {
      List<FaultTriggerViewModelGet> faults = new();
      faultManager.GetList().ForEach(x => faults.Add(new FaultTriggerViewModelGet(x)));
      return Task.FromResult(faults);
    }

    [HttpPost("add")]
    public Task AddFault(FaultTriggerViewModelPost data)
    {
      var fault = data.ToDomainObject();
      faultManager.Add(fault);
      return Task.CompletedTask;
    }

    [HttpPost("remove/{id}")]
    public Task AddFault(string id)
    {
      faultManager.Remove(id);
      return Task.CompletedTask;
    }

    [HttpPost("clearall")]
    public Task ClearFaults()
    {
      faultManager.Clear();
      return Task.CompletedTask;
    }
  }
}
