﻿using System;
using System.Threading.Tasks;
using Arcus.Observability.Correlation;
using Arcus.Observability.Tests.Integration.Fixture;
using Microsoft.Azure.ApplicationInsights.Query.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Arcus.Observability.Tests.Integration.Serilog.Sinks.ApplicationInsights
{
    public class ExceptionTests : ApplicationInsightsSinkTests
    {
        public ExceptionTests(ITestOutputHelper outputWriter) : base(outputWriter)
        {
        }
        
        [Fact]
        public async Task LogException_SinksToApplicationInsights_ResultsInExceptionTelemetry()
        {
            // Arrange
            string message = BogusGenerator.Lorem.Sentence();
            string expectedProperty = BogusGenerator.Lorem.Word();
            var exception = new TestException(message) { SpyProperty = expectedProperty };
            
            // Act
            Logger.LogCritical(exception, exception.Message);

            // Assert
            await RetryAssertUntilTelemetryShouldBeAvailableAsync(async client =>
            {
                EventsExceptionResult[] results = await client.GetExceptionsAsync();
                AssertX.Any(results, result =>
                {
                    Assert.Equal(exception.Message, result.Exception.OuterMessage);
                    Assert.DoesNotContain($"Exception-{nameof(TestException.SpyProperty)}", result.CustomDimensions.Keys);
                });
            });
        }

        [Fact]
        public async Task LogException_SinksToApplicationInsightsWithIncludedProperties_ResultsInExceptionTelemetry()
        {
            // Arrange
            string message = BogusGenerator.Lorem.Sentence();
            string expectedProperty = BogusGenerator.Lorem.Word();
            var exception = new TestException(message) { SpyProperty = expectedProperty };
            ApplicationInsightsSinkOptions.Exception.IncludeProperties = true;
            
            // Act
            Logger.LogCritical(exception, exception.Message);

            // Assert
            await RetryAssertUntilTelemetryShouldBeAvailableAsync(async client =>
            {
                EventsExceptionResult[] results = await client.GetExceptionsAsync();
                AssertX.Any(results, result =>
                {
                    Assert.Equal(exception.Message, result.Exception.OuterMessage);
                    Assert.True(result.CustomDimensions.TryGetValue($"Exception-{nameof(TestException.SpyProperty)}", out string actualProperty));
                    Assert.Equal(expectedProperty, actualProperty);
                });
            });
        }

        [Fact]
        public async Task LogExceptionWithCustomPropertyFormat_SinksToApplicationInsights_ResultsInExceptionTelemetry()
        {
            // Arrange
            string message = BogusGenerator.Lorem.Sentence();
            string expectedProperty = BogusGenerator.Lorem.Word();
            var exception = new TestException(message) { SpyProperty = expectedProperty };
            string propertyFormat = "Exception.{0}";
            ApplicationInsightsSinkOptions.Exception.IncludeProperties = true;
            ApplicationInsightsSinkOptions.Exception.PropertyFormat = propertyFormat;
            
            // Act
            Logger.LogCritical(exception, exception.Message);

            // Assert
            await RetryAssertUntilTelemetryShouldBeAvailableAsync(async client =>
            {
                EventsExceptionResult[] results = await client.GetExceptionsAsync();
                AssertX.Any(results, result =>
                {
                    string propertyName = String.Format(propertyFormat, nameof(TestException.SpyProperty));
                    
                    Assert.Equal(exception.Message, result.Exception.OuterMessage);
                    Assert.True(result.CustomDimensions.TryGetValue(propertyName, out string actualProperty));
                    Assert.Equal(expectedProperty, actualProperty);
                });
            });
        }

        [Fact]
        public async Task LogExceptionWithComponentName_SinksToApplicationInsights_ResultsInTelemetryWithComponentName()
        {
            // Arrange
            string message = BogusGenerator.Lorem.Sentence();
            string componentName = BogusGenerator.Commerce.ProductName();
            var exception = new PlatformNotSupportedException(message);
            LoggerConfiguration.Enrich.WithComponentName(componentName);
            
            // Act
            Logger.LogCritical(exception, exception.Message);

            // Assert
            await RetryAssertUntilTelemetryShouldBeAvailableAsync(async client =>
            {
                EventsExceptionResult[] results = await client.GetExceptionsAsync();
                AssertX.Any(results, result =>
                {
                    Assert.Equal(exception.Message, result.Exception.OuterMessage);
                    Assert.Equal(componentName, result.Cloud.RoleName);
                });
            });
        }

        [Fact]
        public async Task LogExceptionWithCorrelationInfo_SinksToApplicationInsights_ResultsInTelemetryWithCorrelationInfo()
        {
            // Arrange
            string message = BogusGenerator.Lorem.Sentence();
            var exception = new PlatformNotSupportedException(message);
            
            string operationId = $"operation-{Guid.NewGuid()}";
            string transactionId = $"transaction-{Guid.NewGuid()}";
            string operationParentId = $"operation-parent-{Guid.NewGuid()}";
            
            var correlationInfoAccessor = new DefaultCorrelationInfoAccessor();
            correlationInfoAccessor.SetCorrelationInfo(new CorrelationInfo(operationId, transactionId, operationParentId));
            LoggerConfiguration.Enrich.WithCorrelationInfo(correlationInfoAccessor);
            
            // Act
            Logger.LogCritical(exception, exception.Message);

            // Assert
            await RetryAssertUntilTelemetryShouldBeAvailableAsync(async client =>
            {
                EventsExceptionResult[] results = await client.GetExceptionsAsync();
                AssertX.Any(results, result =>
                {
                    Assert.Equal(exception.Message, result.Exception.OuterMessage);
                    Assert.Equal(transactionId, result.Operation.Id);
                    Assert.Equal(operationId, result.Operation.ParentId);
                });
            });
        }
    }
}