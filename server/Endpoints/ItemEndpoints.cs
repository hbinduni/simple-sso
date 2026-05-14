using System.Security.Claims;
using Server.Common;
using Server.Data;
using Server.Models;

namespace Server.Endpoints;

public static class ItemEndpoints
{
    public static void MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/items").RequireAuthorization("access");

        group.MapGet("", ListItems);
        group.MapGet("/{id}", GetItem);
        group.MapPost("", CreateItem);
        group.MapPut("/{id}", UpdateItem);
        group.MapDelete("/{id}", DeleteItem);
    }

    private static async Task<IResult> ListItems(HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var list = await items.GetUserItemsAsync(userId);
        return Results.Ok(ApiResponse.Ok(list));
    }

    private static async Task<IResult> GetItem(string id, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var item = await items.GetByIdAsync(id);
        if (item is null)
            return Results.Json(ApiResponse.Error("Item not found"),
                statusCode: StatusCodes.Status404NotFound);
        if (item.UserId != userId)
            return Results.Json(ApiResponse.Error("Access denied"),
                statusCode: StatusCodes.Status403Forbidden);

        return Results.Ok(ApiResponse.Ok(item));
    }

    private static async Task<IResult> CreateItem(ItemRequest req, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.BadRequest(ApiResponse.Error("Title is required"));

        var item = new Item
        {
            Id = TypeId.NewItemId(),
            UserId = userId,
            Title = req.Title!,
            Description = req.Description ?? "",
            Status = req.Status ?? ItemStatus.Active,
        };
        await items.CreateAsync(item);

        return Results.Json(ApiResponse.Ok(item), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateItem(
        string id, ItemRequest req, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var item = await items.GetByIdAsync(id);
        if (item is null)
            return Results.Json(ApiResponse.Error("Item not found"),
                statusCode: StatusCodes.Status404NotFound);
        if (item.UserId != userId)
            return Results.Json(ApiResponse.Error("Access denied"),
                statusCode: StatusCodes.Status403Forbidden);

        if (!string.IsNullOrEmpty(req.Title))
            item.Title = req.Title;
        if (!string.IsNullOrEmpty(req.Description))
            item.Description = req.Description;
        if (req.Status is { } status)
            item.Status = status;

        await items.UpdateAsync(item);
        return Results.Ok(ApiResponse.Ok(item));
    }

    private static async Task<IResult> DeleteItem(string id, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var item = await items.GetByIdAsync(id);
        if (item is null)
            return Results.Json(ApiResponse.Error("Item not found"),
                statusCode: StatusCodes.Status404NotFound);
        if (item.UserId != userId)
            return Results.Json(ApiResponse.Error("Access denied"),
                statusCode: StatusCodes.Status403Forbidden);

        await items.DeleteAsync(id);
        return Results.Ok(ApiResponse.Ok(new { message = "Item deleted successfully" }));
    }
}
