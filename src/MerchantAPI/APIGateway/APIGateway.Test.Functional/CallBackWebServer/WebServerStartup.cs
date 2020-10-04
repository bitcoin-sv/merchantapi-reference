// Copyright (c) 2020 Bitcoin Association

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MerchantAPI.APIGateway.Test.Functional.CallBackWebServer
{

  /// <summary>
  /// Startup class use to configure web server
  /// </summary>
  public class StressTestStartup
  {
    IConfiguration Configuration;

    public StressTestStartup(IConfiguration configuration)
    {
      Configuration = configuration;
    }
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddControllers().AddJsonOptions(options => { options.JsonSerializerOptions.WriteIndented = true; });
      services.AddSingleton<RouteToCallBackController>();
    }


    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      string url = Configuration["callback::url"];
      var uri = new Uri(url);

      app.UseRouting();


      app.UseEndpoints(endpoints =>
      {
        // There are several ways how to receive callbacks at dynamic URL:
        //  - use Low level MapPost(lambda) - to low level
        //  - use Rewrite middleware
        //  - Use MapControllerRoute (has some problems)
        //  - use MapDynamicControllerRoute - we use this one

        endpoints.MapDynamicControllerRoute<RouteToCallBackController>(uri.AbsolutePath);

      });
    }
  }

}

