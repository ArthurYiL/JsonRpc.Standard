﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JsonRpc.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JsonRpc.AspNetCore
{
    /// <summary>
    /// An interface used to configure JSON RPC server.
    /// </summary>
    public interface IJsonRpcBuilder
    {
        /// <summary>
        /// Adds a JSON-RPC service to the built <see cref="IJsonRpcServiceHost"/>.
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> of a class that inherits from <see cref="JsonRpcService"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="serviceType"/> does not inherit from <see cref="JsonRpcService"/>.</exception>
        IJsonRpcBuilder Register(Type serviceType);

        /// <summary>
        /// Adds a middleware to intercept the JSON RPC requests.
        /// </summary>
        /// <param name="middleware">The middleware to be added.</param>
        /// <remarks>
        /// <para>If there are multiple calls to this method, the handler generated by the first applied middleware
        /// will be the fist to receive the request.</para>
        /// <para>Usually <see cref="Intercept(Func{RequestContext,Func{Task},Task})"/> is preferred to this overload.</para>
        /// </remarks>
        IJsonRpcBuilder Intercept(Func<RequestHandler, RequestHandler> middleware);

    }

    internal class JsonRpcBuilder : IJsonRpcBuilder
    {

        private readonly JsonRpcServiceHostBuilder serviceHostBuilder;
        private readonly IServiceCollection serviceCollection;
        private readonly bool injectServices;

        public JsonRpcBuilder(JsonRpcOptions options, IServiceCollection serviceCollection)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            this.serviceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));
            injectServices = options.InjectServiceTypes;
            serviceHostBuilder = new JsonRpcServiceHostBuilder
            {
                ContractResolver = options.ContractResolver,
                LoggerFactory = options.LoggerFactory,
                ServiceFactory = options.ServiceFactory,
                MethodBinder = options.MethodBinder
            };
        }

        public IJsonRpcServiceHost BuildServiceHost(IServiceProvider serviceProvider)
        {
            lock (serviceHostBuilder)
            {
                var useDefaultLoggerFactory = serviceHostBuilder.LoggerFactory == null;
                if (useDefaultLoggerFactory)
                    serviceHostBuilder.LoggerFactory = serviceProvider.GetService<ILoggerFactory>();
                try
                {
                    return serviceHostBuilder.Build();
                }
                finally
                {
                    if (useDefaultLoggerFactory) serviceHostBuilder.LoggerFactory = null;
                }
            }
        }

        IJsonRpcBuilder IJsonRpcBuilder.Register(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            lock (serviceHostBuilder)
                serviceHostBuilder.Register(serviceType);
            if (injectServices) serviceCollection.AddTransient(serviceType);
            return this;
        }

        /// <inheritdoc />
        public IJsonRpcBuilder Intercept(Func<RequestHandler, RequestHandler> middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            lock (serviceHostBuilder)
                serviceHostBuilder.Intercept(middleware);
            return this;
        }
    }

    public static class JsonRpcBuilderExtensions
    {

        /// <summary>
        /// Adds a JSON-RPC service to the built <see cref="IJsonRpcServiceHost"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        public static IJsonRpcBuilder Register<TService>(this IJsonRpcBuilder builder) where TService : JsonRpcService
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.Register(typeof(TService));
            return builder;
        }


        /// <summary>
        /// Adds all the public JSON-RPC service types in the assembly to the built <see cref="IJsonRpcServiceHost"/>.
        /// </summary>
        /// <param name="assembly">The assembly to search services in.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <c>null</c>.</exception>
        public static IJsonRpcBuilder Register(this IJsonRpcBuilder builder, Assembly assembly)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            foreach (var t in assembly.ExportedTypes
                .Where(t => typeof(JsonRpcService).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo())))
                builder.Register(t);
            return builder;
        }

        /// <summary>
        /// Adds all the public JSON-RPC service types in the assembly of specified <see cref="Type"/>
        /// to the built <see cref="IJsonRpcServiceHost"/>.
        /// </summary>
        /// <typeparam name="T">A type. The search will be performed in the assembly where this type is in.</typeparam>
        public static IJsonRpcBuilder RegisterFromAssembly<T>(this IJsonRpcBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.Register(typeof(T).GetTypeInfo().Assembly);
            return builder;
        }

        /// <summary>
        /// Adds a handler to intercept the JSON RPC requests.
        /// </summary>
        /// <param name="handler">The handler to be added.</param>
        /// <remarks>
        /// If there are multiple calls to this method, the last handler applied will be
        /// the fist to receive the request.
        /// </remarks>
        public static void Intercept(this IJsonRpcBuilder builder, Func<RequestContext, Func<Task>, Task> handler)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            builder.Intercept(next => (context => handler(context, () => next(context))));
        }
    }
}