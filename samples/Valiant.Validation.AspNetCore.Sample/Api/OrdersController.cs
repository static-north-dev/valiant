using Microsoft.AspNetCore.Mvc;
using Valiant.Validation.AspNetCore.Sample.Models;

namespace Valiant.Validation.AspNetCore.Sample.Api;

[ApiController]
[Route("mvc/orders")]
public sealed class OrdersController(IValidator<CreateOrder> validator) : ControllerBase
{
    [HttpPost]
    public IActionResult Create(CreateOrder order)
    {
        var result = validator.Validate(order);
        return result.IsValid
            ? Ok(order)
            : BadRequest(result.ToMvcValidationProblemDetails());
    }
}
