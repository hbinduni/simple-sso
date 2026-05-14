namespace Server.Common;

/// <summary>Extracts the client IP, honouring proxy headers (X-Forwarded-For, X-Real-IP).</summary>
public static class ClientIp
{
    public static string? Get(HttpContext ctx)
    {
        var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xff))
        {
            var comma = xff.IndexOf(',');
            return comma >= 0 ? xff[..comma].Trim() : xff.Trim();
        }

        var xri = ctx.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xri))
            return xri;

        return ctx.Connection.RemoteIpAddress?.ToString();
    }
}
