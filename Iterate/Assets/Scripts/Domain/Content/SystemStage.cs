using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// One stage in a System's ordered sequence, carrying exactly the reference its kind admits. A
    /// route-selection stage expands to the chosen route's shop, the Active Branch configuration and
    /// the route's Process; that expansion belongs to Session Flow, not to this record.
    /// </summary>
    /// <param name="Kind">Which stage kind this is.</param>
    /// <param name="Process">The Process a Process stage runs; null for every other kind.</param>
    /// <param name="Shop">The shop a shop stage opens; null for every other kind.</param>
    /// <param name="Routes">The routes a route-selection stage offers; null or empty for every other kind.</param>
    public sealed record SystemStage(
        SystemStageKind Kind,
        ProcessID? Process,
        ShopID? Shop,
        IReadOnlyList<RouteID> Routes
    )
    {
        /// <summary>
        /// The Process a Process stage runs; null for every other kind. Validated against the stage
        /// kind's whole field shape at construction.
        /// </summary>
        public ProcessID? Process { get; } = RequireShape(
            Kind,
            Process,
            Shop,
            Routes
        );

        /// <summary>
        /// Validates that a stage carries exactly the reference its kind admits and returns the
        /// Process reference unchanged.
        /// </summary>
        /// <param name="kind">The declared stage kind.</param>
        /// <param name="process">The candidate Process reference.</param>
        /// <param name="shop">The candidate shop reference.</param>
        /// <param name="routes">The candidate route list.</param>
        /// <returns>The Process reference unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the fields do not match the kind's shape.</exception>
        private static ProcessID? RequireShape(
            SystemStageKind kind,
            ProcessID? process,
            ShopID? shop,
            IReadOnlyList<RouteID> routes
        )
        {
            bool hasRoutes = routes != null && routes.Count > 0;

            switch (kind)
            {
                case SystemStageKind.Process when !process.HasValue:
                    throw new ArgumentException("A Process stage requires a Process reference.", nameof(process));
                
                case SystemStageKind.Process when shop.HasValue:
                    throw new ArgumentException("A Process stage must not carry a shop reference.", nameof(shop));
                
                case SystemStageKind.Process when hasRoutes:
                    throw new ArgumentException("A Process stage must not carry routes.", nameof(routes));
                
                case SystemStageKind.Process:
                    return process;
                
                case SystemStageKind.Shop when !shop.HasValue:
                    throw new ArgumentException("A shop stage requires a shop reference.", nameof(shop));
                
                case SystemStageKind.Shop when process.HasValue:
                    throw new ArgumentException("A shop stage must not carry a Process reference.", nameof(process));
                
                case SystemStageKind.Shop when hasRoutes:
                    throw new ArgumentException("A shop stage must not carry routes.", nameof(routes));
                
                case SystemStageKind.Shop:
                    return process;
            }

            if (!hasRoutes)
                throw new ArgumentException("A route-selection stage requires at least one route.", nameof(routes));

            if (process.HasValue)
                throw new ArgumentException("A route-selection stage must not carry a Process reference.", nameof(process));

            if (shop.HasValue)
                throw new ArgumentException("A route-selection stage must not carry a shop reference.", nameof(shop));

            return process;
        }
    }
}