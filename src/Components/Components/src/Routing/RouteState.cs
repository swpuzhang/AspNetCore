// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Components.Routing
{
    /// <summary>
    /// Infrastructure to manage routing state for components.
    /// <para>
    /// This type is an internal API that supports Components infrastructure and is not designed
    /// for use by application code.
    /// </para>
    /// </summary>
    public sealed class RouteState
    {
        private static readonly char[] QueryOrHashStartChar = new[] { '?', '#' };

        /// <summary>
        /// Initializes <see cref="RouteState" /> to use <see cref="RouteTable" />.
        /// </summary>
        /// <param name="routes"></param>
        /// <param name="baseUri"></param>
        internal void Initialize(RouteTable routes, string baseUri)
        {
            IsInitialized = true;

            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            BaseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        }

        internal bool IsInitialized { get; private set; }

        internal string BaseUri { get; private set; }

        internal RouteTable Routes { get; private set; }

        /// <summary>
        /// Determines if the current path can be handled.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><see langword="true"/> if this path can be handled, otherwise <see langword="false"/>.</returns>
        public bool CanHandleRoute(string path)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"Cannot invoke {nameof(CanHandleRoute)} before initialization.");
            }

            var context = GetRouteContext(path);
            Routes.Route(context);

            return context.Handler != null;
        }

        internal RouteContext GetRouteContext(string path)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"Cannot invoke {nameof(GetRouteContext)} before initialization.");
            }

            // Strip out the query string or fragment portion from the URI. We do not need these for routing purposes.
            // http://mysite.com/dir1/component?display=1 -> http://mysite.com/dir1/component
            // http://mysite.com/dir1/component#index -> http://mysite.com/dir1/component
            path = StringUntilAny(path, QueryOrHashStartChar);

            // Convert the URI to a relative path:
            // http://mysite.com/dir1/component -> component
            var locationPath = ToBaseRelativePath(BaseUri, path);

            return new RouteContext(locationPath);
        }

        private static string ToBaseRelativePath(string baseUri, string locationAbsolute)
        {
            if (locationAbsolute.StartsWith(baseUri, StringComparison.Ordinal))
            {
                // The absolute URI must be of the form "{baseUri}something" (where
                // baseUri ends with a slash), and from that we return "something"
                return locationAbsolute.Substring(baseUri.Length);
            }
            else if ($"{locationAbsolute}/".Equals(baseUri, StringComparison.Ordinal))
            {
                // Special case: for the base URI "/something/", if you're at
                // "/something" then treat it as if you were at "/something/" (i.e.,
                // with the trailing slash). It's a bit ambiguous because we don't know
                // whether the server would return the same page whether or not the
                // slash is present, but ASP.NET Core at least does by default when
                // using PathBase.
                return string.Empty;
            }

            var message = $"The URI '{locationAbsolute}' is not contained by the base URI '{baseUri}'.";
            throw new ArgumentException(message);
        }

        private static string StringUntilAny(string str, char[] chars)
        {
            var firstIndex = str.IndexOfAny(chars);
            return firstIndex < 0
                ? str
                : str.Substring(0, firstIndex);
        }
    }
}
