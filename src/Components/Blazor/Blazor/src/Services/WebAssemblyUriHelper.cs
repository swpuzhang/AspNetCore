// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using Interop = Microsoft.AspNetCore.Components.Browser.BrowserUriHelperInterop;

namespace Microsoft.AspNetCore.Blazor.Services
{
    /// <summary>
    /// Default client-side implementation of <see cref="IUriHelper"/>.
    /// </summary>
    public class WebAssemblyUriHelper : UriHelperBase
    {
        /// <summary>
        /// Gets the instance of <see cref="WebAssemblyUriHelper"/>.
        /// </summary>
        public static readonly WebAssemblyUriHelper Instance = new WebAssemblyUriHelper();

        // For simplicity we force public consumption of the BrowserUriHelper through
        // a singleton. Only a single instance can be updated by the browser through
        // interop. We can construct instances for testing.
        internal WebAssemblyUriHelper()
        {
        }

        internal RouteState RouteState { get; } = new RouteState();

        protected override void EnsureInitialized()
        {
            // As described in the comment block above, WebAssemblyUriHelper is only for
            // client-side (Mono) use, so it's OK to rely on synchronicity here.
            var baseUri = WebAssemblyJSRuntime.Instance.Invoke<string>(Interop.GetBaseUri);
            var uri = WebAssemblyJSRuntime.Instance.Invoke<string>(Interop.GetLocationHref);
            InitializeState(uri, baseUri);
        }

        /// <inheritdoc />
        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            WebAssemblyJSRuntime.Instance.Invoke<object>(Interop.NavigateTo, uri, forceLoad);
        }

        /// <summary>
        /// For framework use only.
        /// </summary>
        [JSInvokable(nameof(NotifyLocationChanged))]
        public static void NotifyLocationChanged(string uriAbsolute, bool interceptedLink)
        {
            if (interceptedLink)
            {
                // UriHelper intercepted a browser location change. If the Router cannot handle this, we must force a navigation
                // so that the user can get to the location they needed to get to.
                if (!Instance.RouteState.CanHandleRoute(uriAbsolute))
                {
                    // We do not have an entry corresponding to the incoming route. That is, this is not a component.
                    // Perform a regular browser navigation instead.
                    Instance.NavigateTo(uriAbsolute, forceLoad: true);
                    return;
                }
            }

            Instance.SetAbsoluteUri(uriAbsolute);
            Instance.TriggerOnLocationChanged();
        }

        /// <inheritdoc />
        protected override void EnableLocationChangeEvents()
        {
            WebAssemblyJSRuntime.Instance.Invoke<object>(
                Interop.EnableNavigationInterception,
                typeof(WebAssemblyUriHelper).Assembly.GetName().Name,
                nameof(NotifyLocationChanged));
        }
    }
}
