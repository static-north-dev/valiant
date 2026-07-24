using Microsoft.AspNetCore.Mvc;
using Valiant.Validation.AspNetCore.Samples.Orders;

namespace Valiant.Validation.AspNetCore.Samples.Mvc;

/// <summary>
/// Handles orders after automatic MVC validation has succeeded.
/// </summary>
[ApiController]
[Route("mvc/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    public IActionResult Create(CreateOrder order)
    {
        return Created($"/mvc/orders/{order.CustomerEmail}", order);
    }
}
