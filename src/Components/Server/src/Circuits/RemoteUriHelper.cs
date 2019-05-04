// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Interop = Microsoft.AspNetCore.Components.Browser.BrowserUriHelperInterop;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    /// <summary>
    /// A Server-Side Components implementation of <see cref="IUriHelper"/>.
    /// </summary>
    public class RemoteUriHelper : UriHelperBase
    {
        private readonly ILogger<RemoteUriHelper> _logger;
        private IJSRuntime _jsRuntime;
        private bool _enableNavigationInterception;

        /// <summary>
        /// Creates a new <see cref="RemoteUriHelper"/> instance.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/>.</param>
        public RemoteUriHelper(ILogger<RemoteUriHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets or sets whether the circuit has an attached <see cref="IJSRuntime"/>.
        /// </summary>
        public bool HasAttachedJSRuntime => _jsRuntime != null;

        /// <summary>
        /// Initializes the <see cref="RemoteUriHelper"/>.
        /// </summary>
        /// <param name="uriAbsolute">The absolute URI of the current page.</param>
        /// <param name="baseUriAbsolute">The absolute base URI of the current page.</param>
        public override void InitializeState(string uriAbsolute, string baseUriAbsolute)
        {
            base.InitializeState(uriAbsolute, baseUriAbsolute);
            TriggerOnLocationChanged();
        }

        /// <summary>
        /// Initializes the <see cref="RemoteUriHelper"/>.
        /// </summary>
        /// <param name="jsRuntime">The <see cref="IJSRuntime"/> to use for interoperability.</param>
        internal void AttachJsRuntime(IJSRuntime jsRuntime)
        {
            if (_jsRuntime != null)
            {
                throw new InvalidOperationException("JavaScript runtime already initialized.");
            }
            _jsRuntime = jsRuntime;
            _logger.LogDebug($"{nameof(RemoteUriHelper)} initialized.");

            if (_enableNavigationInterception)
            {
                EnableNavigationInterception();
            }
        }

        /// <summary>
        /// For framework use only.
        /// </summary>
        [JSInvokable(nameof(NotifyLocationChanged))]
        public static void NotifyLocationChanged(string uriAbsolute, bool interceptedLink)
        {
            var circuit = CircuitHost.Current;
            if (circuit == null)
            {
                var message = $"{nameof(NotifyLocationChanged)} called without a circuit.";
                throw new InvalidOperationException(message);
            }

            var uriHelper = (RemoteUriHelper)circuit.Services.GetRequiredService<IUriHelper>();
            if (interceptedLink)
            {
                // UriHelper intercepted a browser location change. If the Router cannot handle this, we must force a navigation
                // so that the user can get to the location they needed to get to.
                var routing = circuit.Services.GetRequiredService<RouteState>();
                if (!routing.CanHandleRoute(uriAbsolute))
                {
                    // We do not have an entry corresponding to the incoming route. That is, this is not a component.
                    // Perform a regular browser navigation instead.
                    uriHelper.NavigateTo(uriAbsolute, forceLoad: true);
                    return;
                }
            }

            uriHelper.SetAbsoluteUri(uriAbsolute);
            uriHelper._logger.LogTrace($"Location changed to '{uriAbsolute}'.");
            uriHelper.TriggerOnLocationChanged();
        }

        /// <inheritdoc />
        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            _logger.LogTrace($"{uri} force load {forceLoad}.");

            if (_jsRuntime == null)
            {
                throw new InvalidOperationException("Navigation commands can not be issued at this time. This is because the component is being " +
                    "prerendered and the page has not yet loaded in the browser or because the circuit is currently disconnected. " +
                    "Components must wrap any navigation calls in conditional logic to ensure those navigation calls are not " +
                    "attempted during prerendering or while the client is disconnected.");
            }
            _jsRuntime.InvokeAsync<object>(Interop.NavigateTo, uri, forceLoad);
        }

        /// <inheritdoc />
        protected override void EnableLocationChangeEvents()
        {
            _enableNavigationInterception = true;

            if (HasAttachedJSRuntime)
            {
                EnableNavigationInterception();
            }
        }

        private void EnableNavigationInterception()
        {
            _jsRuntime.InvokeAsync<object>(
                Interop.EnableNavigationInterception,
                typeof(RemoteUriHelper).Assembly.GetName().Name,
                nameof(NotifyLocationChanged));
        }
    }
}
