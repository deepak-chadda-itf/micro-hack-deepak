using Microsoft.AspNetCore.Mvc;
using OctoSupply.Api.Models;
using OctoSupply.Api.Repositories;

namespace OctoSupply.Api.Controllers;

[ApiController]
[Route("api/cart")]
public sealed class CartController(CartRepository repository) : ControllerBase
{
    private readonly CartRepository _repository = repository;

    [HttpGet("{cartId:guid}")]
    public async Task<ActionResult<Cart>> Get(Guid cartId) =>
        Ok(await _repository.FindAsync(cartId));

    [HttpPost("{cartId:guid}/items")]
    public async Task<ActionResult<Cart>> AddItem(Guid cartId, [FromBody] AddCartItemRequest request) =>
        Ok(await _repository.AddItemAsync(cartId, request.ProductId, request.Quantity));

    [HttpPut("{cartId:guid}/items/{productId:int}")]
    public async Task<ActionResult<Cart>> UpdateItem(
        Guid cartId,
        int productId,
        [FromBody] UpdateCartItemRequest request) =>
        Ok(await _repository.UpdateItemAsync(cartId, productId, request.Quantity));

    [HttpDelete("{cartId:guid}/items/{productId:int}")]
    public async Task<ActionResult<Cart>> RemoveItem(Guid cartId, int productId) =>
        Ok(await _repository.RemoveItemAsync(cartId, productId));
}