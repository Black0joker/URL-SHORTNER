using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace URLShortener.Infrastructure.RateLimiting;

/// <summary>
/// Factory that creates distributed sliding-window rate limit partitions.
/// The partition key encodes policy + client identity (IP or user id); the counter
/// state lives in Redis, so limits hold across every API instance.
/// </summary>
internal static class RedisSlidingWindowPartitioner
{
    public static Func<HttpContext, RateLimitPartition<string>> Create(
        string policyName,
        RateLimitPolicyOptions policy,
        RedisRateLimitStore store,
        bool failOpen,
        ILogger logger)
    {
        return httpContext =>
        {
            var partitionKey = ResolvePartitionKey(policyName, policy, httpContext);

            var metadata = new RedisSlidingWindowMetadata(
                partitionKey,
                policy.PermitLimit,
                TimeSpan.FromSeconds(policy.WindowSeconds));

            return RateLimitPartition.Get(partitionKey,
                _ => new RedisSlidingWindowRateLimiter(store, metadata, failOpen, logger));
        };
    }

    private static string ResolvePartitionKey(
        string policyName, RateLimitPolicyOptions policy, HttpContext httpContext)
    {
        if (string.Equals(policy.PartitionBy, "user", StringComparison.OrdinalIgnoreCase))
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(userId))
                return $"{policyName}:u:{userId}";
        }

        return $"{policyName}:ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
