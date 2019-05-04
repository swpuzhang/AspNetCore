// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Layouts;
using Microsoft.AspNetCore.Components.RenderTree;

namespace Microsoft.AspNetCore.Components.Routing
{
    /// <summary>
    /// A component that displays whichever other component corresponds to the
    /// current navigation location.
    /// </summary>
    public class Router : IComponent, IDisposable
    {
        private RenderHandle _renderHandle;
        private string _locationAbsolute;

        [Inject] private IUriHelper UriHelper { get; set; }

        [Inject] private RouteState RouteState { get; set; }

        /// <summary>
        /// Gets or sets the assembly that should be searched, along with its referenced
        /// assemblies, for components matching the URI.
        /// </summary>
        [Parameter] public Assembly AppAssembly { get; private set; }
        
        /// <summary>
        /// Gets or sets the content to render when no match is found for the requested route.
        /// </summary>
        [Parameter] public RenderFragment NotFoundContent { get; private set; }

        /// <inheritdoc />
        public void Configure(RenderHandle renderHandle)
        {
            _renderHandle = renderHandle;
            _locationAbsolute = UriHelper.GetAbsoluteUri();
            UriHelper.OnLocationChanged += OnLocationChanged;
        }

        /// <inheritdoc />
        public Task SetParametersAsync(ParameterCollection parameters)
        {
            parameters.SetParameterProperties(this);
            var types = ComponentResolver.ResolveComponents(AppAssembly);

            var routes = RouteTable.Create(types);
            RouteState.Initialize(routes, UriHelper.GetBaseUri());

            Refresh();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            UriHelper.OnLocationChanged -= OnLocationChanged;
        }

        /// <inheritdoc />
        protected virtual void Render(RenderTreeBuilder builder, Type handler, IDictionary<string, object> parameters)
        {
            builder.OpenComponent(0, typeof(LayoutDisplay));
            builder.AddAttribute(1, LayoutDisplay.NameOfPage, handler);
            builder.AddAttribute(2, LayoutDisplay.NameOfPageParameters, parameters);
            builder.CloseComponent();
        }

        private void Refresh()
        {
            var context = RouteState.GetRouteContext(_locationAbsolute);
            RouteState.Routes.Route(context);
            if (context.Handler != null)
            {
                _renderHandle.Render(builder => Render(builder, context.Handler, context.Parameters));
            }
            else if (NotFoundContent != null)
            {
                _renderHandle.Render(NotFoundContent);
            }
            else
            {
                throw new InvalidOperationException($"'{nameof(Router)}' cannot find any component with a route for '/{context.Path}', and {nameof(NotFoundContent)} is not specified.");
            }
        }

        private void OnLocationChanged(object sender, string newAbsoluteUri)
        {
            _locationAbsolute = newAbsoluteUri;
            if (_renderHandle.IsInitialized && RouteState.IsInitialized)
            {
                Refresh();
            }
        }
    }
}
