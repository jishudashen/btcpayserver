#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Services;
using System.Linq;
using System.Threading;
using BTCPayServer.Abstractions.Constants;

namespace BTCPayServer.Controllers.GreenField;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldReportsController : Controller
{
    public GreenfieldReportsController(
        ApplicationDbContextFactory dbContextFactory,
        ReportService reportService)
    {
        DBContextFactory = dbContextFactory;
        ReportService = reportService;
    }
    public ApplicationDbContextFactory DBContextFactory { get; }
    public ReportService ReportService { get; }

    public const string DefaultReport = "Invoices";
    [Authorize(Policy = Policies.CanViewReports, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    [HttpPost("~/api/v1/stores/{storeId}/reports")]
    [NonAction] // Disabling this endpoint as we still need to figure out the request/response model
    public async Task<IActionResult> StoreReports(string storeId, [FromBody] StoreReportRequest? vm = null, CancellationToken cancellationToken = default)
    {
        vm ??= new StoreReportRequest();
        vm.ViewName ??= DefaultReport;
        vm.TimePeriod ??= new TimePeriod();
        vm.TimePeriod.To ??= DateTime.UtcNow;
        vm.TimePeriod.From ??= vm.TimePeriod.To.Value.AddMonths(-1);
        var from = vm.TimePeriod.From.Value;
        var to = vm.TimePeriod.To.Value;

        if (ReportService.ReportProviders.TryGetValue(vm.ViewName, out var report))
        {
            if (!report.IsAvailable())
                return this.CreateAPIError(503, "view-unavailable", $"This view is unavailable at this moment");

            var ctx = new Services.Reporting.QueryContext(storeId, from, to);
            await report.Query(ctx, cancellationToken);
            ResizeRowsIfNeeded(ctx.ViewDefinition?.Fields.Count ?? 0, ctx.Data);
            var result = new StoreReportResponse
            {
                Fields = ctx.ViewDefinition?.Fields ?? new List<StoreReportResponse.Field>(),
                Charts = ctx.ViewDefinition?.Charts ?? new List<ChartDefinition>(),
                Data = ctx.Data.Select(d => new JArray(d)).ToList(),
                From = from,
                To = to
            };
            return Json(result);
        }

        ModelState.AddModelError(nameof(vm.ViewName), "View doesn't exist");
        return this.CreateValidationError(ModelState);
    }

    // Make sure all the rows are dimensioned to the number of fields
    private void ResizeRowsIfNeeded(int fieldsCount, IList<IList<object?>> ctxData)
    {
        foreach (var row in ctxData)
        {
            var dummyCount = fieldsCount - row.Count;
            if (dummyCount > 0)
            {
                for (var i = 0; i < dummyCount; i++)
                    row.Add(null);
            }
            if (dummyCount < 0)
                for (var i = 0; i < -dummyCount; i++)
                    row.RemoveAt(row.Count - 1);
        }
    }
}

