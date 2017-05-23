﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Manages a collection of features.
    /// </summary>
    public interface IFeatureCollection
    {
        /// <summary>
        /// Gets the feature of the specified type.
        /// </summary>
        /// <param name="featureType">Feature type. Usually an interface type.</param>
        /// <returns>The requested feature instance, or <c>null</c> if not applicable.</returns>
        object Get(Type featureType);

        /// <summary>
        /// Puts the specified feature instance into the dictionary.
        /// </summary>
        /// <param name="featureType">Feature type. Usually an interface type.</param>
        /// <param name="instance">Feature instance, or <c>null</c> to set an existing feature to its default.</param>
        void Set(Type featureType, object instance);
    }

    /// <summary>
    /// Extension methods for <see cref="IFeatureCollection"/>.
    /// </summary>
    public static class FeatureCollectionExtensions
    {
        /// <summary>
        /// Gets the feature of the specified type.
        /// </summary>
        /// <typeparam name="TFeature">Feature type. Usually an interface type.</typeparam>
        /// <param name="featureCollection">The target feature collection.</param>
        /// <returns>The requested feature instance, or <c>null</c> if not applicable.</returns>
        public static TFeature Get<TFeature>(this IFeatureCollection featureCollection)
        {
            if (featureCollection == null) throw new ArgumentNullException(nameof(featureCollection));
            return (TFeature) featureCollection.Get(typeof(TFeature));
        }

        /// <summary>
        /// Puts the specified feature instance into the feature collection.
        /// </summary>
        /// <typeparam name="TFeature">Feature type. Usually an interface type.</typeparam>
        /// <param name="featureCollection">The target feature collection.</param>
        /// <param name="instance">Feature instance, or <c>null</c> to remove an existing feature.</param>
        public static void Set<TFeature>(this IFeatureCollection featureCollection, TFeature instance)
        {
            if (featureCollection == null) throw new ArgumentNullException(nameof(featureCollection));
            featureCollection.Set(typeof(TFeature), instance);
        }
    }

    /// <summary>
    /// A collection of features.
    /// </summary>
    public class FeatureCollection : IFeatureCollection
    {
        private readonly IFeatureCollection baseCollection;
        private IDictionary<Type, object> myDict;

        public FeatureCollection() : this(null)
        {
        }

        public FeatureCollection(IFeatureCollection baseCollection)
        {
            this.baseCollection = baseCollection;
        }

        /// <inheritdoc />
        public object Get(Type featureType)
        {
            if (myDict != null && myDict.TryGetValue(featureType, out var value))
                return value;
            return baseCollection?.Get(featureType);
        }

        /// <inheritdoc />
        public void Set(Type featureType, object instance)
        {
            if (featureType == null) throw new ArgumentNullException(nameof(featureType));
            if (instance == null && myDict != null)
            {
                // If featureType exists in baseCollection, we just reset it to base's default.
                myDict.Remove(featureType);
                return;
            }
            if (myDict == null) myDict = new Dictionary<Type, object>();
            AssertKindOf(featureType, instance, nameof(instance));
            myDict[featureType] = instance;
        }

        private void AssertKindOf(Type type, object instance, string paramName)
        {
            Debug.Assert(type != null);
            if (instance == null || !type.GetTypeInfo().IsAssignableFrom(instance.GetType().GetTypeInfo()))
                throw new ArgumentException("Instance is not corresponding to given type.", paramName);
        }

    }
}
