using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.Serialization.SystemTextJson;

using System.Text.Json;
using System.IO;
using Amazon.Lambda.SimpleEmailEvents;
using Amazon.Lambda.SimpleEmailEvents.Actions;

namespace SESMailRelay.Tests
{
    public class FunctionTest
    {
        [Fact]
        public async Task TestToUpperFunctionAsync()
        {

            // Invoke the lambda function and confirm the string was upper cased.
            var context = new TestLambdaContext();
            using var file = File.OpenText("ses-email-receive.json");
            var input = JsonSerializer.Deserialize<SimpleEmailEvent<LambdaReceiptAction>>(file.ReadToEnd(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            try
            {
                await Function.Handler(input, context);
            }
            catch (Exception e)
            {
                Assert.Null(e);
            }
        }
    }
}
