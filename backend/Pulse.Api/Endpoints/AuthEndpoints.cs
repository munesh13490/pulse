namespace Pulse.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost(
            "/v1/auth/login",
            async (LoginRequest req, IUserIdentityService ids, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(req.Username) ||
                    req.Username.Length > 200 ||
                    string.IsNullOrEmpty(req.Password) ||
                    req.Password.Length > 256)
                {
                    return Results.BadRequest();
                }

                var pair = await ids.Login(req.Username, req.Password, ct);
                return pair is null ? Results.Unauthorized() : Results.Ok(pair);
            })
            .Produces<TokenPair>()
            .Produces(401);

        app.MapPost(
            "/v1/auth/refresh",
            async (RefreshRequest req, IUserIdentityService ids, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(req.RefreshToken) ||
                    req.RefreshToken.Length > 256)
                {
                    return Results.BadRequest();
                }

                var pair = await ids.Refresh(req.RefreshToken, ct);
                return pair is null ? Results.Unauthorized() : Results.Ok(pair);
            })
            .Produces<TokenPair>()
            .Produces(401);

        app.MapPost(
            "/v1/auth/logout",
            async (RefreshRequest req, IUserIdentityService ids, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(req.RefreshToken) ||
                    req.RefreshToken.Length > 256)
                {
                    return Results.BadRequest();
                }

                await ids.Logout(req.RefreshToken, ct);
                return Results.NoContent();
            });

        app.MapGet(
            "/v1/auth/me",
            async (HttpContext ctx, IUserIdentityService ids, CancellationToken ct) =>
                Results.Ok(await RequestIdentity.Actor(ctx, ids, ct)))
            .RequireAuthorization();
    }
}
