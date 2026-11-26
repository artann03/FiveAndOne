using FiveAndOne.API.Providers;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Services.Payments;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace FiveAndOne.API.Controllers;

/// <summary>
/// Payment provider callbacks. The webhook is the ONLY way a payment becomes successful (story 43);
/// the frontend redirect after checkout is never trusted.
/// </summary>
[ApiController]
[Route("api/payments")]
[AllowAnonymous]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentWebhookService _webhooks;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public PaymentsController(IPaymentWebhookService webhooks, ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _webhooks = webhooks;
        _db = db;
        _environment = environment;
    }

    /// <summary>Provider webhook (signature verified by the gateway implementation).</summary>
    [HttpPost("webhook")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        await _webhooks.HandleAsync(body, headers, ct);
        return Ok();
    }

    /// <summary>DEVELOPMENT ONLY: the mock provider's checkout page (the checkoutUrl of mock payments).</summary>
    [HttpGet("mock/{paymentId:guid}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> MockCheckout(Guid paymentId, CancellationToken ct)
    {
        EnsureDevelopment();
        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId, ct) ?? throw new NotFoundException("Payment not found.");

        var html = $$"""
            <!doctype html><html><head><meta charset="utf-8"><title>Mock checkout</title>
            <style>body{font-family:system-ui;max-width:420px;margin:60px auto;padding:0 16px}button{padding:10px 18px;margin-right:8px;cursor:pointer}</style></head>
            <body><h2>Mock payment (development)</h2>
            <p>{{WebUtility.HtmlEncode(payment.Purpose.ToString())}} payment: <b>{{payment.Amount:0.00}} {{WebUtility.HtmlEncode(payment.Currency)}}</b></p>
            <p>Status: <b id="s">{{payment.Status}}</b></p>
            <button onclick="go(true)">Pay</button><button onclick="go(false)">Fail</button>
            <script>
            async function go(ok){const r=await fetch('/api/payments/mock/{{paymentId}}/complete?succeeded='+ok,{method:'POST'});
            document.getElementById('s').textContent=r.ok?(ok?'Succeeded':'Failed'):'Error '+r.status;}
            </script></body></html>
            """;
        return Content(html, "text/html");
    }

    /// <summary>DEVELOPMENT ONLY: simulates the provider webhook for a mock payment (pay or fail).</summary>
    [HttpPost("mock/{paymentId:guid}/complete")]
    public async Task<IActionResult> MockComplete(Guid paymentId, [FromQuery] bool succeeded = true, CancellationToken ct = default)
    {
        EnsureDevelopment();
        await _webhooks.SimulateAsync(paymentId, succeeded, ct);
        return NoContent();
    }

    private void EnsureDevelopment()
    {
        if (!_environment.IsDevelopment())
        {
            throw new NotFoundException("Not found.");
        }
    }
}
