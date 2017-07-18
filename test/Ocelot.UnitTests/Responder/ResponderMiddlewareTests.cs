﻿using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ocelot.Infrastructure.RequestData;
using Ocelot.Logging;
using Ocelot.Responder;
using Ocelot.Responder.Middleware;
using Ocelot.Responses;
using TestStack.BDDfy;
using Xunit;
using Microsoft.AspNetCore.Builder;
using Shouldly;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Ocelot.Errors;
using System.Net;
using Ocelot.Headers;

namespace Ocelot.UnitTests.Responder
{
    public class ResponderMiddlewareTests : ServerHostedMiddlewareTest
    {
        private readonly IHttpResponder _responder;
        private readonly Mock<IRequestScopedDataRepository> _scopedRepository;
        private readonly Mock<IErrorsToHttpStatusCodeMapper> _codeMapper;
        private readonly Mock<IRemoveOutputHeaders> _outputHeaderRemover;
        private HttpStatusCode _httpStatusFromController;
        private string _contentFromController;

        private OkResponse<HttpResponseMessage> _response;
        private List<Error> _pipelineErrors;

        public ResponderMiddlewareTests()
        {
            _outputHeaderRemover = new Mock<IRemoveOutputHeaders>();
            _responder = new HttpContextResponder(_outputHeaderRemover.Object);
            _scopedRepository = new Mock<IRequestScopedDataRepository>();
            _codeMapper = new Mock<IErrorsToHttpStatusCodeMapper>();

            GivenTheTestServerIsConfigured();
        }

        [Fact]
        public void PipelineErrors()
        {
            var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.Continue);

            this.Given(x => x.GivenTheIncomingHttpResponseMessageIs(new HttpResponseMessage()))
                .And(x => x.GivenThereArePipelineErrors())
                .And(x => x.GivenTheErrorWillBeMappedToAnHttpStatus())
                .When(x => x.WhenICallTheMiddleware())
                .Then(x => x.ThenThereAreErrors())
                .BDDfy();
        }

        [Fact]
        public void NoPipelineErrors()
        {
            this.Given(x => x.GivenTheIncomingHttpResponseMessageIs(new HttpResponseMessage()))
                .And(x => x.GivenThereAreNoPipelineErrors())
                .When(x => x.WhenICallTheMiddleware())
                .Then(x => x.ThenThereAreNoErrors())
                .BDDfy();
        }

        protected override void GivenTheTestServerServicesAreConfigured(IServiceCollection services)
        {
            services.AddSingleton<IOcelotLoggerFactory, AspDotNetLoggerFactory>();
            services.AddLogging();
            services.AddSingleton(_codeMapper.Object);
            services.AddSingleton(_responder);
            services.AddSingleton(_scopedRepository.Object);
        }

        protected override void GivenTheTestServerPipelineIsConfigured(IApplicationBuilder app)
        {
            app.UseResponderMiddleware();
            app.Run(SetControllerResponse);
        }

        private async Task SetControllerResponse(HttpContext context)
        {
            _httpStatusFromController = HttpStatusCode.OK;
            _contentFromController = "test response";
            context.Response.StatusCode = (int)_httpStatusFromController;
            await context.Response.WriteAsync(_contentFromController);
        }

        private void GivenThereAreNoPipelineErrors()
        {
            GivenThereArePipelineErrors(new List<Error>());
        }

        private void GivenThereArePipelineErrors()
        {
            GivenThereArePipelineErrors(new List<Error>() { new AnyError() });
        }

        private void GivenThereArePipelineErrors(List<Error> pipelineErrors)
        {
            _pipelineErrors = pipelineErrors;

            _scopedRepository
                .Setup(x => x.Get<bool>("OcelotMiddlewareError"))
                .Returns(new OkResponse<bool>(_pipelineErrors.Count != 0));

            _scopedRepository
                .Setup(sr => sr.Get<List<Error>>("OcelotMiddlewareErrors"))
                .Returns(new OkResponse<List<Error>>(_pipelineErrors));
        }

        private void GivenTheIncomingHttpResponseMessageIs(HttpResponseMessage response)
        {
            _response = new OkResponse<HttpResponseMessage>(response);
            _scopedRepository
                .Setup(x => x.Get<HttpResponseMessage>(It.IsAny<string>()))
                .Returns(_response);
        }

        private void GivenTheErrorWillBeMappedToAnHttpStatus()
        {
            _codeMapper.Setup(cm => cm.Map(_pipelineErrors))
                .Returns((int)HttpStatusCode.InternalServerError);
        }

        private void ThenThereAreNoErrors()
        {
            ResponseMessage.StatusCode.ShouldBe(_httpStatusFromController);
            ResponseMessage.Content.ReadAsStringAsync().Result.ShouldBe(_contentFromController);
        }

        private void ThenThereAreErrors()
        {
            ResponseMessage.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        }
    }
}
