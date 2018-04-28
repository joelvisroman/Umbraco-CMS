﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors.ValueConverters;

namespace Umbraco.Web.Models
{
    /// <summary>
    /// Provide an abstract base class for <c>IPublishedContent</c> implementations.
    /// </summary>
    /// <remarks>This base class does which (a) consitently resolves and caches the Url, (b) provides an implementation
    /// for this[alias], and (c) provides basic content set management.</remarks>
    [DebuggerDisplay("Content Id: {Id}, Name: {Name}")]
    public abstract class PublishedContentBase : IPublishedContent
    {
        private string _url;

        #region ContentType

        public abstract PublishedContentType ContentType { get; }

        #endregion

        #region PublishedElement

        /// <inheritdoc />
        public abstract Guid Key { get; }

        #endregion

        #region PublishedContent

        /// <inheritdoc />
        public abstract int Id { get; }

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <inheritdoc />
        public abstract string UrlName { get; }

        /// <inheritdoc />
        public abstract int SortOrder { get; }

        /// <inheritdoc />
        public abstract int Level { get; }

        /// <inheritdoc />
        public abstract string Path { get; }

        /// <inheritdoc />
        public abstract int TemplateId { get; }

        /// <inheritdoc />
        public abstract int CreatorId { get; }

        /// <inheritdoc />
        public abstract string CreatorName { get; }

        /// <inheritdoc />
        public abstract DateTime CreateDate { get; }

        /// <inheritdoc />
        public abstract int WriterId { get; }

        /// <inheritdoc />
        public abstract string WriterName { get; }

        /// <inheritdoc />
        public abstract DateTime UpdateDate { get; }

        /// <inheritdoc />
        /// <remarks>
        /// The url of documents are computed by the document url providers. The url of medias are, at the moment,
        /// computed here from the 'umbracoFile' property -- but we should move to media url providers at some point.
        /// </remarks>
        public virtual string Url
        {
            // fixme contextual!
            get
            {
                // should be thread-safe although it won't prevent url from being resolved more than once
                if (_url != null)
                    return _url; // fixme very bad idea with nucache? or?

                switch (ItemType)
                {
                    case PublishedItemType.Content:
                        if (UmbracoContext.Current == null)
                            throw new InvalidOperationException(
                                "Cannot resolve a Url for a content item when UmbracoContext.Current is null.");
                        if (UmbracoContext.Current.UrlProvider == null)
                            throw new InvalidOperationException(
                                "Cannot resolve a Url for a content item when UmbracoContext.Current.UrlProvider is null.");
                        _url = UmbracoContext.Current.UrlProvider.GetUrl(Id);
                        break;
                    case PublishedItemType.Media:
                        var prop = GetProperty(Constants.Conventions.Media.File);
                        if (prop == null || prop.GetValue() == null)
                        {
                            _url = string.Empty;
                            return _url;
                        }

                        var propType = ContentType.GetPropertyType(Constants.Conventions.Media.File);

                        // fixme this is horrible we need url providers for media too
                        //This is a hack - since we now have 2 properties that support a URL: upload and cropper, we need to detect this since we always
                        // want to return the normal URL and the cropper stores data as json
                        switch (propType.EditorAlias)
                        {
                            case Constants.PropertyEditors.Aliases.UploadField:
                                _url = prop.GetValue().ToString();
                                break;
                            case Constants.PropertyEditors.Aliases.ImageCropper:
                                //get the url from the json format

                                var stronglyTyped = prop.GetValue() as ImageCropperValue;
                                if (stronglyTyped != null)
                                {
                                    _url = stronglyTyped.Src;
                                    break;
                                }
                                _url = prop.GetValue()?.ToString();
                                break;
                        }
                        break;
                    default:
                        throw new NotSupportedException();
                }

                return _url;
            }
        }
  
        /// <inheritdoc />
        public abstract PublishedCultureInfos GetCulture(string culture = ".");

        /// <inheritdoc />
        public abstract IReadOnlyDictionary<string, PublishedCultureInfos> Cultures { get; }

        /// <inheritdoc />
        public abstract PublishedItemType ItemType { get; }

        /// <inheritdoc />
        public abstract bool IsDraft { get; }

        #endregion

        #region Tree

        /// <inheritdoc />
        public abstract IPublishedContent Parent { get; }

        /// <inheritdoc />
        public abstract IEnumerable<IPublishedContent> Children { get; }

        #endregion

        #region Properties

        /// <inheritdoc cref="IPublishedElement.Properties"/>
        public abstract IEnumerable<IPublishedProperty> Properties { get; }

        /// <inheritdoc cref="IPublishedElement.GetProperty(string)"/>
        public abstract IPublishedProperty GetProperty(string alias);

        /// <inheritdoc cref="IPublishedContent.GetProperty(string, bool)"/>
        public virtual IPublishedProperty GetProperty(string alias, bool recurse)
        {
            // fixme - but can this work with variants?

            var property = GetProperty(alias);
            if (recurse == false) return property;

            IPublishedContent content = this;
            var firstNonNullProperty = property;
            while (content != null && (property == null || property.HasValue() == false))
            {
                content = content.Parent;
                property = content?.GetProperty(alias);
                if (firstNonNullProperty == null && property != null) firstNonNullProperty = property;
            }

            // if we find a content with the property with a value, return that property
            // if we find no content with the property, return null
            // if we find a content with the property without a value, return that property
            //   have to save that first property while we look further up, hence firstNonNullProperty

            return property != null && property.HasValue() ? property : firstNonNullProperty;
        }

        #endregion
    }
}
