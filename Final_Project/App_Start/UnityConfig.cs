using System;
using Unity;
using Unity.AspNet.Mvc;
using Final_Project.Models;
using System.Web.Mvc;
using Unity.Lifetime;

namespace Final_Project
{
    public static class UnityConfig
    {
        #region Unity Container
        private static Lazy<IUnityContainer> container = new Lazy<IUnityContainer>(() =>
        {
            var container = new UnityContainer();
            RegisterTypes(container);
            return container;
        });

        public static IUnityContainer Container => container.Value;
        #endregion

        public static void RegisterTypes(IUnityContainer container)
        {
            // Register your RestaurantContext (Database context)
            container.RegisterType<RestaurantContext>(new HierarchicalLifetimeManager());
        }

        public static void Initialize()
        {
            var container = UnityConfig.Container;
            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
        }
    }
}