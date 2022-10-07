// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MerchantAPI.APIGateway.Rest.Swagger
{
  public class FaultControllerVisibility : IActionModelConvention
  {
    public void Apply(ActionModel action)
    {
      if (action.Controller.ControllerName.ToLower().Contains("fault"))
      {
        action.ApiExplorer.IsVisible = false;
      }
    }
  }
}
