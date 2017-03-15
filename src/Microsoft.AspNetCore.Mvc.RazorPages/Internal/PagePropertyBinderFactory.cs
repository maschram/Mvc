// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Internal
{
    public static class PagePropertyBinderFactory
    {
        public static Func<Page, object, Task> GetModelBinderFactory(
            IModelMetadataProvider modelMetadataProvider,
            CompiledPageActionDescriptor actionDescriptor)
        {
            if (modelMetadataProvider == null)
            {
                throw new ArgumentNullException(nameof(modelMetadataProvider));
            }

            if (actionDescriptor == null)
            {
                throw new ArgumentNullException(nameof(actionDescriptor));
            }

            var bindPropertiesOnPage = actionDescriptor.ModelTypeInfo == null;
            var propertiesToBind = GetPropertiesToBind(
                modelMetadataProvider,
                bindPropertiesOnPage ? actionDescriptor.PageTypeInfo : actionDescriptor.ModelTypeInfo);

            if (propertiesToBind.Count == 0)
            {
                return null;
            }

            return (page, model) =>
            {
                var binder = page.Binder;
                var pageContext = page.PageContext;
                var instance = bindPropertiesOnPage ? page : model;

                return BindPropertiesAsync(binder, pageContext, instance, propertiesToBind);
            };
        }

        private static async Task BindPropertiesAsync(
            PageArgumentBinder binder,
            PageContext pageContext,
            object instance,
            IList<PropertyBindingInfo> propertiesToBind)
        {
            for (var i = 0; i < propertiesToBind.Count; i++)
            {
                var propertyBindingInfo = propertiesToBind[i];
                var modelBindingResult = await binder.BindAsync(pageContext, propertyBindingInfo.ParameterDescriptor);
                if (modelBindingResult.IsModelSet)
                {
                    var modelMetadata = propertyBindingInfo.ModelMetadata;
                    PropertyValueSetter.SetValue(
                        propertyBindingInfo.ModelMetadata,
                        propertyBindingInfo.PropertyHelper,
                        instance,
                        modelBindingResult.Model);
                }
            }
        }

        private static IList<PropertyBindingInfo> GetPropertiesToBind(
            IModelMetadataProvider modelMetadataProvider,
            TypeInfo handlerSource)
        {
            var properties = PropertyHelper.GetProperties(type: handlerSource.AsType());
            if (properties.Length == 0)
            {
                return EmptyArray<PropertyBindingInfo>.Instance;
            }

            var propertyBindingInfo = new List<PropertyBindingInfo>();
            for (var i = 0; i < properties.Length; i++)
            {
                var propertyHelper = properties[i];
                var property = propertyHelper.Property;
                var attributes = property.GetCustomAttributes(inherit: true);
                var bindingInfo = BindingInfo.GetBindingInfo(attributes);
                if (bindingInfo == null)
                {
                    continue;
                }

                var parameterDescriptor = new ParameterDescriptor
                {
                    BindingInfo = bindingInfo,
                    Name = propertyHelper.Name,
                    ParameterType = property.PropertyType,
                };

                var modelMetadata = modelMetadataProvider.GetMetadataForType(property.PropertyType);
                propertyBindingInfo.Add(new PropertyBindingInfo(propertyHelper, parameterDescriptor, modelMetadata));
            }

            return propertyBindingInfo;
        }

        private struct PropertyBindingInfo
        {
            public PropertyBindingInfo(
                PropertyHelper helper,
                ParameterDescriptor parameterDescriptor,
                ModelMetadata modelMetadata)
            {
                PropertyHelper = helper;
                ParameterDescriptor = parameterDescriptor;
                ModelMetadata = modelMetadata;
            }

            public PropertyHelper PropertyHelper { get; }

            public ParameterDescriptor ParameterDescriptor { get; }

            public ModelMetadata ModelMetadata { get; }
        }
    }
}
