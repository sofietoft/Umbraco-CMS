﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.PropertyEditors.ValueConverters;
using Umbraco.Core.Services;
using Umbraco.Web.PublishedCache;

namespace Umbraco.Web.PropertyEditors.ValueConverters
{

    /// <summary>
    /// The multi node tree picker property editor value converter.
    /// </summary>
    [DefaultPropertyValueConverter(typeof(MustBeStringValueConverter))]
    public class MultiNodeTreePickerValueConverter : PropertyValueConverterBase
    {
        private readonly IFacadeAccessor _facadeAccessor;

        private static readonly List<string> PropertiesToExclude = new List<string>
        {
            Constants.Conventions.Content.InternalRedirectId.ToLower(CultureInfo.InvariantCulture),
            Constants.Conventions.Content.Redirect.ToLower(CultureInfo.InvariantCulture)
        };

        public MultiNodeTreePickerValueConverter(IFacadeAccessor facadeAccessor)
        {
            _facadeAccessor = facadeAccessor ?? throw new ArgumentNullException(nameof(facadeAccessor));
        }

        public override bool IsConverter(PublishedPropertyType propertyType)
        {
            return propertyType.PropertyEditorAlias.Equals(Constants.PropertyEditors.MultiNodeTreePickerAlias)
                || propertyType.PropertyEditorAlias.Equals(Constants.PropertyEditors.MultiNodeTreePicker2Alias);
        }

        public override PropertyCacheLevel GetPropertyCacheLevel(PublishedPropertyType propertyType)
            => PropertyCacheLevel.Facade;

        public override Type GetPropertyValueType(PublishedPropertyType propertyType)
            => typeof (IEnumerable<IPublishedContent>);

        public override object ConvertSourceToInter(IPropertySet owner, PublishedPropertyType propertyType, object source, bool preview)
        {
            if (propertyType.PropertyEditorAlias.Equals(Constants.PropertyEditors.MultiNodeTreePickerAlias))
            {
                var nodeIds = source.ToString()
                    .Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToArray();
                return nodeIds;
            }
            if (propertyType.PropertyEditorAlias.Equals(Constants.PropertyEditors.MultiNodeTreePicker2Alias))
            {
                var nodeIds = source.ToString()
                    .Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Udi.Parse)
                    .ToArray();
                return nodeIds;
            }
            return null;
        }

        public override object ConvertInterToObject(IPropertySet owner, PublishedPropertyType propertyType, PropertyCacheLevel cacheLevel, object source, bool preview)
        {
            if (source == null)
            {
                return null;
            }

            //TODO: Inject an UmbracoHelper and create a GetUmbracoHelper method based on either injected or singleton
            if (UmbracoContext.Current != null)
            {
                if (propertyType.PropertyEditorAlias.Equals(Constants.PropertyEditors.MultiNodeTreePickerAlias))
                {
                    var nodeIds = (int[])source;

                    if ((propertyType.PropertyTypeAlias != null && PropertiesToExclude.InvariantContains(propertyType.PropertyTypeAlias)) == false)
                    {
                        var multiNodeTreePicker = new List<IPublishedContent>();

                        var objectType = UmbracoObjectTypes.Unknown;

                        foreach (var nodeId in nodeIds)
                        {
                            var multiNodeTreePickerItem =
                                GetPublishedContent(nodeId, ref objectType, UmbracoObjectTypes.Document, id => _facadeAccessor.Facade.ContentCache.GetById(id))
                                ?? GetPublishedContent(nodeId, ref objectType, UmbracoObjectTypes.Media, id => _facadeAccessor.Facade.MediaCache.GetById(id))
                                ?? GetPublishedContent(nodeId, ref objectType, UmbracoObjectTypes.Member, id => _facadeAccessor.Facade.MemberCache.GetById(id));

                            if (multiNodeTreePickerItem != null)
                            {
                                multiNodeTreePicker.Add(multiNodeTreePickerItem);
                            }
                        }

                        return multiNodeTreePicker;
                    }

                    // return the first nodeId as this is one of the excluded properties that expects a single id
                    return nodeIds.FirstOrDefault();
                }

                if (propertyType.PropertyEditorAlias.Equals(Constants.PropertyEditors.MultiNodeTreePicker2Alias))
                {
                    var udis = (Udi[])source;

                    if ((propertyType.PropertyTypeAlias != null && PropertiesToExclude.InvariantContains(propertyType.PropertyTypeAlias)) == false)
                    {
                        var multiNodeTreePicker = new List<IPublishedContent>();

                        var objectType = UmbracoObjectTypes.Unknown;

                        foreach (var udi in udis)
                        {
                            var guidUdi = udi as GuidUdi;
                            if (guidUdi == null) continue;
                            var multiNodeTreePickerItem =
                                GetPublishedContent(udi, ref objectType, UmbracoObjectTypes.Document, id => _facadeAccessor.Facade.ContentCache.GetById(guidUdi.Guid))
                                ?? GetPublishedContent(udi, ref objectType, UmbracoObjectTypes.Media, id => _facadeAccessor.Facade.MediaCache.GetById(guidUdi.Guid))
                                ?? GetPublishedContent(udi, ref objectType, UmbracoObjectTypes.Member, id => _facadeAccessor.Facade.MemberCache.GetByProviderKey(guidUdi.Guid));
                            if (multiNodeTreePickerItem != null)
                            {
                                multiNodeTreePicker.Add(multiNodeTreePickerItem);
                            }
                        }

                        return multiNodeTreePicker;
                    }

                    // return the first nodeId as this is one of the excluded properties that expects a single id
                    return udis.FirstOrDefault();
                }
            }
            return source;
        }

        /// <summary>
        /// Attempt to get an IPublishedContent instance based on ID and content type
        /// </summary>
        /// <param name="nodeId">The content node ID</param>
        /// <param name="actualType">The type of content being requested</param>
        /// <param name="expectedType">The type of content expected/supported by <paramref name="contentFetcher"/></param>
        /// <param name="contentFetcher">A function to fetch content of type <paramref name="expectedType"/></param>
        /// <returns>The requested content, or null if either it does not exist or <paramref name="actualType"/> does not match <paramref name="expectedType"/></returns>
        private IPublishedContent GetPublishedContent<T>(T nodeId, ref UmbracoObjectTypes actualType, UmbracoObjectTypes expectedType, Func<T, IPublishedContent> contentFetcher)
        {
            // is the actual type supported by the content fetcher?
            if (actualType != UmbracoObjectTypes.Unknown && actualType != expectedType)
            {
                // no, return null
                return null;
            }

            // attempt to get the content
            var content = contentFetcher(nodeId);
            if (content != null)
            {
                // if we found the content, assign the expected type to the actual type so we don't have to keep looking for other types of content
                actualType = expectedType;
            }
            return content;
        }
    }
}