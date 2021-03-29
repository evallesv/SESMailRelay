using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SimpleEmailEvents;
using Amazon.Lambda.SimpleEmailEvents.Actions;
using Amazon.S3;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MimeKit;

// This project specifies the serializer used to convert Lambda event into .NET classes in the project's main 
// main function. This assembly register a serializer for use when the project is being debugged using the
// AWS .NET Mock Lambda Test Tool.
[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace SESMailRelay
{
    public class Function
    {
        private static string _from;
        private static string _subjectPrefix;
        private static string _emailBucket;
        private static string _emailKeyPrefix;
        private static string _allowPlusSign;
        private static Dictionary<string, List<string>> _fordwardMapping;
        private static AmazonS3Client _s3;
        private static AmazonSimpleEmailServiceClient _ses;
        /// <summary>
        /// The main entry point for the custom runtime.
        /// </summary>
        /// <param name="args"></param>
        private static async Task Main(string[] args)
        {
            var serializer = new DefaultLambdaJsonSerializer();
            _from = Environment.GetEnvironmentVariable("DOTNET_SMR_FROM");
            _subjectPrefix = Environment.GetEnvironmentVariable("DOTNET_SMR_SUBJECTPREFIX");
            _emailBucket = Environment.GetEnvironmentVariable("DOTNET_SMR_EMAILBUCKET");
            _emailKeyPrefix = Environment.GetEnvironmentVariable("DOTNET_SMR_EMAILKEYPREFIX");
            _allowPlusSign = Environment.GetEnvironmentVariable("DOTNET_SMR_ALLOWPLUSSIGN");
            using (var fwds = GenerateStreamFromString(Environment.GetEnvironmentVariable("DOTNET_SMR_FORWARDMAPPING")))
            {
                _fordwardMapping = serializer.Deserialize<Dictionary<string, List<string>>>(fwds);
            }
            _s3 = new AmazonS3Client();
            _ses = new AmazonSimpleEmailServiceClient();
            Func<SimpleEmailEvent<LambdaReceiptAction>, ILambdaContext, Task> func = Handler;
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper(func, serializer);
            using var bootstrap = new LambdaBootstrap(handlerWrapper);
            await bootstrap.RunAsync();
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        ///
        /// To use this handler to respond to an AWS event, reference the appropriate package from 
        /// https://github.com/aws/aws-lambda-dotnet#events
        /// and change the string input parameter to the desired event type.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task Handler(SimpleEmailEvent<LambdaReceiptAction> input, ILambdaContext context)
        {
            Console.WriteLine($"Config: from: {_from}");
            Console.WriteLine($"Config: subjectPrefix: {_subjectPrefix}");
            Console.WriteLine($"Config: emailBucket: {_emailBucket}");
            Console.WriteLine($"Config: emailKeyPrefix: {_emailKeyPrefix}");
            Console.WriteLine($"Config: allowPlusSign: {_allowPlusSign}");
            Console.WriteLine($"Config: fordwardMapping: {string.Join(";", _fordwardMapping.Select(p => p.Key + ":" + string.Join(",", p.Value)))}");
            foreach (var sesRecord in input.Records)
            {
                Console.WriteLine($"[{sesRecord.EventSource} {sesRecord.Ses.Mail.Timestamp}] Message = {sesRecord.Ses.Mail.MessageId} from: {sesRecord.Ses.Mail.CommonHeaders.From[0]}");
                foreach (var dominio in _fordwardMapping.Keys)
                {
                    if (sesRecord.Ses.Mail.Destination.Any(d => d.EndsWith(dominio)))
                    {
                        var obj = await _s3.GetObjectAsync(_emailBucket, _emailKeyPrefix + sesRecord.Ses.Mail.MessageId);
                        var receivedMessage = MimeMessage.Load(obj.ResponseStream);

                        var sendmail = await _ses.SendEmailAsync(new SendEmailRequest(
                            _from,
                            new Destination() { ToAddresses = _fordwardMapping[dominio] },
                            new Message(
                                new Content($"{_subjectPrefix}{sesRecord.Ses.Mail.CommonHeaders.Subject}"),
                                new Body()
                                {
                                    Html = new Content($"Recibido de: {sesRecord.Ses.Mail.CommonHeaders.From[0]}<br/><br/>{receivedMessage.HtmlBody}"),
                                    Text = new Content($"Recibido de: {sesRecord.Ses.Mail.CommonHeaders.From[0]}\r\n\r\n{receivedMessage.TextBody}")
                                })));
                    }
                }
            };

        }


        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
