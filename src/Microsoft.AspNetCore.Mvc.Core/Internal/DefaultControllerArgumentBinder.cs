// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    /// <summary>
    /// Provides a default implementation of <see cref="IControllerArgumentBinder"/>.
    /// Uses ModelBinding to populate action parameters.
    /// </summary>
    public class DefaultControllerArgumentBinder : IControllerArgumentBinder
    {
        private readonly ParameterBinder _parameterBinder;

        public DefaultControllerArgumentBinder(ParameterBinder parameterBinder)
        {
            if (parameterBinder == null)
            {
                throw new ArgumentNullException(nameof(parameterBinder));
            }

            _parameterBinder = parameterBinder;
        }

        public Task BindArgumentsAsync(
            ControllerContext controllerContext,
            object controller,
            IDictionary<string, object> arguments)
        {
            if (controllerContext == null)
            {
                throw new ArgumentNullException(nameof(controllerContext));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (controllerContext.ActionDescriptor == null)
            {
                throw new ArgumentException(Resources.FormatPropertyOfTypeCannotBeNull(
                    nameof(ControllerContext.ActionDescriptor),
                    nameof(ControllerContext)));
            }

            // Perf: Avoid allocating async state machines where possible. We only need the state
            // machine if you need to bind properties.
            var actionDescriptor = controllerContext.ActionDescriptor;
            if (actionDescriptor.BoundProperties.Count == 0 &&
                actionDescriptor.Parameters.Count == 0)
            {
                return TaskCache.CompletedTask;
            }

            return BindArgumentsCoreAsync(controllerContext, controller, arguments);
        }

        private async Task BindArgumentsCoreAsync(
            ControllerContext controllerContext,
            object controller,
            IDictionary<string, object> arguments)
        {
            var valueProvider = await CompositeValueProvider.CreateAsync(controllerContext);

            var parameters = controllerContext.ActionDescriptor.Parameters;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];

                var result = await BindModelAsync(parameter, controllerContext, valueProvider);
                if (result.IsModelSet)
                {
                    arguments[parameter.Name] = result.Model;
                }
            }

            var properties = controllerContext.ActionDescriptor.BoundProperties;
            if (properties.Count == 0)
            {
                // Perf: Early exit to avoid PropertyHelper lookup in the (common) case where we have no
                // bound properties.
                return;
            }

            var propertyHelpers = PropertyHelper.GetProperties(controller);
            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];

                var result = await BindModelAsync(property, controllerContext, valueProvider);
                if (result.IsModelSet)
                {
                    var propertyHelper = FindPropertyHelper(propertyHelpers, property);
                    if (propertyHelper != null)
                    {
                        var metadata = _parameterBinder.ModelMetadataProvider.GetMetadataForType(property.ParameterType);
                        PropertyValueSetter.SetValue(metadata, propertyHelper, controller, result.Model);
                    }
                }
            }
        }

        public async Task<ModelBindingResult> BindModelAsync(
            ParameterDescriptor parameter,
            ControllerContext controllerContext)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (controllerContext == null)
            {
                throw new ArgumentNullException(nameof(controllerContext));
            }

            var valueProvider = await CompositeValueProvider.CreateAsync(controllerContext);

            return await BindModelAsync(parameter, controllerContext, valueProvider);
        }

        public Task<ModelBindingResult> BindModelAsync(
           ParameterDescriptor parameter,
           ControllerContext controllerContext,
           IValueProvider valueProvider)
        {
            return _parameterBinder.BindModelAsync(parameter, controllerContext, valueProvider);
        }

        private static PropertyHelper FindPropertyHelper(PropertyHelper[] propertyHelpers, ParameterDescriptor property)
        {
            for (var i = 0; i < propertyHelpers.Length; i++)
            {
                var propertyHelper = propertyHelpers[i];
                if (string.Equals(propertyHelper.Name, property.Name, StringComparison.Ordinal))
                {
                    return propertyHelper;
                }
            }

            return null;
        }
    }
}
