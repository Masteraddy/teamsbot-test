
using System.Collections.Concurrent;
using System.Net;
using APITest.Authentication;
using APITest.Constants;
using APITest.Models;
using APITest.Util;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Client;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Graph.Communications.Resources;
using Microsoft.Graph.Contracts;
using Microsoft.Graph.Models;
using Microsoft.Skype.Bots.Media;

namespace APITest.Bot
{
    public class BotServices : IDisposable, IBotService
    {
        public ICommunicationsClient Client { get; private set; }
        public IGraphLogger logger;
        private readonly IGraphLogger _graphLogger;
        private readonly ILogger _logger;
        private readonly AppSettings _settings;
        private readonly IBotMediaLogger _mediaPlatformLogger;

        public ConcurrentDictionary<string, CallHandler> CallHandlers { get; } = new ConcurrentDictionary<string, CallHandler>();

        public BotServices(
            IGraphLogger graphLogger,
            ILogger<BotServices> logger,
            IOptions<AppSettings> settings,
            IBotMediaLogger mediaLogger)
        {
            // Bot services
            _settings = settings.Value;
            _graphLogger = graphLogger;
            _logger = logger;
            _mediaPlatformLogger = mediaLogger;
        }

        public void Initialize()
        {
            var name = AppConstants.BOTNAME;
            // this.logger = new GraphLogger();
            // Initialize bot services
            var builder = new CommunicationsClientBuilder(
                name,
                _settings.AadAppId
            );

            var authProvider = new AuthenticationProvider(
                name,
                _settings.AadAppId,
               _settings.AadAppSecret,
                _graphLogger
            );

            var mediaPlatformSettings = new MediaPlatformSettings()
            {
                MediaPlatformInstanceSettings = new MediaPlatformInstanceSettings()
                {
                    CertificateThumbprint = _settings.CertificateThumbprint,
                    InstanceInternalPort = _settings.MediaInternalPort,
                    InstancePublicIPAddress = IPAddress.Any,
                    InstancePublicPort = _settings.MediaInstanceExternalPort,
                    ServiceFqdn = _settings.MediaDnsName
                },
                ApplicationId = _settings.AadAppId,
                MediaPlatformLogger = _mediaPlatformLogger
            };

            // var notificationUrl = new Uri("https://4.bot.masteraddy.com.mg/api/calls/callbacks");
            var notificationUrl = new Uri($"https://{_settings.ServiceDnsName}:{_settings.BotInstanceExternalPort}/{HttpRouteConstants.CallSignalingRoutePrefix}/{HttpRouteConstants.OnNotificationRequestRoute}");
            _logger.LogInformation($"Notification URL: {notificationUrl}");

            builder.SetNotificationUrl(notificationUrl)
                .SetAuthenticationProvider(authProvider);
            builder.SetMediaPlatformSettings(mediaPlatformSettings);
            builder.SetServiceBaseUrl(new Uri(AppConstants.PlaceCallEndpointUrl));

            this.Client = builder.Build();
            this.Client.Calls().OnIncoming += this.CallsOnIncoming;
            this.Client.Calls().OnUpdated += this.CallsOnUpdated;
        }

        /// <summary>
        /// Incoming call handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="CollectionEventArgs{TResource}" /> instance containing the event data.</param>
        private void CallsOnIncoming(ICallCollection sender, CollectionEventArgs<ICall> args)
        {
            // Call incoming
            args.AddedResources.ForEach(call =>
            {
                // Get the policy recording parameters.

                // The context associated with the incoming call.
                IncomingContext incomingContext =
                    call.Resource.IncomingContext;

                // The RP participant.
                string observedParticipantId =
                    incomingContext.ObservedParticipantId;

                // If the observed participant is a delegate.
                IdentitySet onBehalfOfIdentity =
                    incomingContext.OnBehalfOf;

                // If a transfer occured, the transferor.
                IdentitySet transferorIdentity =
                    incomingContext.Transferor;

                string countryCode = null;
                EndpointType? endpointType = null;

                // Note: this should always be true for CR calls.
                if (incomingContext.ObservedParticipantId == incomingContext.SourceParticipantId)
                {
                    // The dynamic location of the RP.
                    countryCode = call.Resource.Source.CountryCode;

                    // The type of endpoint being used.
                    endpointType = call.Resource.Source.EndpointType;
                }

                IMediaSession mediaSession = Guid.TryParse(call.Id, out Guid callId)
                    ? this.CreateLocalMediaSession(callId)
                    : this.CreateLocalMediaSession();

                // Answer call
                call?.AnswerAsync(mediaSession).ForgetAndLogExceptionAsync(
                    call.GraphLogger,
                    $"Answering call {call.Id} with scenario {call.ScenarioId}.");
            });
        }

        private void CallsOnUpdated(ICallCollection sender, CollectionEventArgs<ICall> args)
        {
            // Call updated
            foreach (var call in args.AddedResources)
            {
                var callHandler = new CallHandler(call, _settings, _logger);
                var threadId = call.Resource.ChatInfo.ThreadId;
                this.CallHandlers[threadId] = callHandler;
            }

            foreach (var call in args.RemovedResources)
            {
                var threadId = call.Resource.ChatInfo.ThreadId;
                if (this.CallHandlers.TryRemove(threadId, out CallHandler? handler))
                {
                    Task.Run(async () =>
                    {
                        await handler.BotMediaStream.ShutdownAsync();
                        handler.Dispose();
                    });
                }
            }
        }

        public void Dispose()
        {
            // Dispose bot services
            this.Client?.Dispose();
            this.Client = null;
        }

        public async Task Shutdown()
        {
            // this.logger.Warn("Shutting down the bot.");
            _logger.LogWarning("Shutting down the bot.");
            await this.Client.TerminateAsync();
            this.Dispose();
        }

        public async Task<ICall> JoinCallAsync(JoinCallBody joinCallBody)
        {
            // Join call
            var scenarioId = Guid.NewGuid();

            var (chatInfo, meetingInfo) = JoinInfo.ParseJoinURL(joinCallBody.JoinUrl);

            var tenantId = (meetingInfo as OrganizerMeetingInfo).Organizer.GetPrimaryIdentity().GetTenantId();
            var mediaSession = this.CreateLocalMediaSession();

            var joinParams = new JoinMeetingParameters(chatInfo, meetingInfo, mediaSession)
            {
                TenantId = tenantId
            };
            if (!string.IsNullOrEmpty(joinCallBody.DisplayName))
            {
                joinParams.GuestIdentity = new Identity
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = joinCallBody.DisplayName
                };
            }

            if (!this.CallHandlers.TryGetValue(joinParams.ChatInfo.ThreadId, out CallHandler? call))
            {
                var statefulCall = await this.Client.Calls().AddAsync(joinParams, scenarioId).ConfigureAwait(false);
                statefulCall.GraphLogger.Info($"Call creation complete: {statefulCall.Id}");
                return statefulCall;
            }
            throw new Exception("Call has already been added");
        }

        private ILocalMediaSession CreateLocalMediaSession(Guid mediaSessionId = default)
        {
            try
            {
                // create media session object, this is needed to establish call connections
                return this.Client.CreateMediaSession(
                    new AudioSocketSettings
                    {
                        StreamDirections = StreamDirection.Sendrecv,
                        // Note! Currently, the only audio format supported when receiving unmixed audio is Pcm16K
                        SupportedAudioFormat = AudioFormat.Pcm16K,
                        ReceiveUnmixedMeetingAudio = false //get the extra buffers for the speakers
                    },
                    new VideoSocketSettings
                    {
                        StreamDirections = StreamDirection.Inactive
                    },
                    mediaSessionId: mediaSessionId);
            }
            catch (Exception e)
            {
                // this.logger.Error(e.Message);
                _logger.LogError(e.Message);
                throw;
            }
        }
        public async Task EndCallByThreadIdAsync(string threadId)
        {
            string callId = string.Empty;
            try
            {
                var callHandler = this.GetHandlerOrThrow(threadId);
                callId = callHandler.Call.Id;
                await callHandler.Call.DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Manually remove the call from SDK state.
                // This will trigger the ICallCollection.OnUpdated event with the removed resource.
                if (!string.IsNullOrEmpty(callId))
                {
                    this.Client.Calls().TryForceRemove(callId, out ICall _);
                }
            }
        }
        private CallHandler GetHandlerOrThrow(string threadId)
        {
            if (!this.CallHandlers.TryGetValue(threadId, out CallHandler? handler))
            {
                throw new ArgumentException($"call ({threadId}) not found");
            }

            return handler;
        }
    }
}