﻿// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Configuration.Tests.Mocks;
using AppBrix.Tests;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace AppBrix.Configuration.Tests
{
    public sealed class ConfigServiceTests
    {
        #region Tests
        [Fact, Trait(TestCategories.Category, TestCategories.Functional)]
        public void TestGetConfig()
        {
            var provider = new ConfigProviderMock();
            var serializer = new ConfigSerializerMock();
            var service = new ConfigService(provider, serializer);

            service.Get<ConfigMock>();
            provider.ReadConfigs.Should().ContainSingle("the service should have tried to read the config");
            provider.ReadConfigs[0].Should().Be(typeof(ConfigMock), "the read config should be of the requested type");
            provider.WrittenConfigs.Should().BeEmpty("the service should not have tried to write the config");
            serializer.Serialized.Should().BeEmpty("the service should not have tried to serialize the config");
            serializer.Deserialized.Should().ContainSingle("the service should have tried to deserialize the read config");
            serializer.Deserialized[0].Key.Should().Be(typeof(ConfigMock), "the deserialized config should be the same as the read one");

            service.Get<ConfigMock>();
            provider.ReadConfigs.Should().ContainSingle("the service should not have tried to reread the config");
            provider.WrittenConfigs.Should().BeEmpty("the service should not have tried to write the config when returning it a second time");
            serializer.Serialized.Should().BeEmpty("the service should not have tried to serialize the config when returning it a second time");
            serializer.Deserialized.Should().ContainSingle("the service should not have tried to re-deserialize the config");
        }

        [Fact, Trait(TestCategories.Category, TestCategories.Functional)]
        public void TestSaveAllConfigs()
        {
            var provider = new ConfigProviderMock();
            var serializer = new ConfigSerializerMock();
            var service = new ConfigService(provider, serializer);

            service.SaveAll();
            provider.ReadConfigs.Should().BeEmpty("the service should have not tried to read any configs");
            provider.WrittenConfigs.Should().BeEmpty("the service should not have tried to write any configs");
            serializer.Serialized.Should().BeEmpty("the service should not have tried to serialize any configs");
            serializer.Deserialized.Should().BeEmpty("the service should have tried to deserialize any configs");

            service.Get<ConfigMock>();
            service.SaveAll();
            provider.WrittenConfigs.Should().ContainSingle("the service should have tried to write the config");
            provider.WrittenConfigs[0].Key.Should().Be(typeof(ConfigMock), "the written config should be the same as the requested one");
            serializer.Serialized.Should().ContainSingle("the service should have tried to serialize the config");
            serializer.Serialized[0].Key.Should().Be(typeof(ConfigMock), "the serialized config should be the same as the requested one");
            
            service.SaveAll();
            provider.WrittenConfigs.Should().ContainSingle("the service should not have tried to re-write an unmodified config");
        }

        [Fact, Trait(TestCategories.Category, TestCategories.Functional)]
        public void TestSaveConfig()
        {
            var provider = new ConfigProviderMock();
            var serializer = new ConfigSerializerMock();
            var service = new ConfigService(provider, serializer);
            var config = service.Get<ConfigMock>();

            service.Save<ConfigMock>();
            provider.WrittenConfigs.Should().ContainSingle("the service should have tried to write the config");
            provider.WrittenConfigs[0].Key.Should().Be(config.GetType(), "the written config should be the same as the provided one one");
            serializer.Serialized.Should().ContainSingle("the service should have tried to serialize the config");
            serializer.Serialized[0].Key.Should().Be(config.GetType(), "the serialized config type should be the same as the original");
            serializer.Serialized[0].Value.Should().BeSameAs(config, "the serialized config should be the same as the provided one");

            service.Save<ConfigMock>();
            provider.WrittenConfigs.Should().ContainSingle("the service should not have tried to re-write an unmodified config");
        }

        [Fact, Trait(TestCategories.Category, TestCategories.Performance)]
        public void TestPerformanceConfigService()
        {
            TestUtils.TestPerformance(this.TestPerformanceConfigServiceInternal);
        }
        #endregion

        #region Private methods
        private void TestPerformanceConfigServiceInternal()
        {
            var provider = new ConfigProviderMock();
            var serializer = new ConfigSerializerMock();
            var service = new ConfigService(provider, serializer);
            var type = typeof(ConfigMock);

            for (int i = 0; i < 1000000; i++)
            {
                service.Get(type);
            }

            for (int i = 0; i < 500; i++)
            {
                service.SaveAll();
            }
        }
        #endregion
    }
}
