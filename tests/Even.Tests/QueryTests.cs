using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Dsl;
using Xunit;

namespace Even.Tests
{
    public class QueryTests : EvenTestKit
    {
        public class TestQuery { public Guid ID { get; } = Guid.NewGuid(); }
        public class TestResponse { public Guid ID { get; set; } }

        [Fact]
        public async Task Query_using_eventstream_gets_response()
        {
            var responder = Sys.ActorOf(conf =>
            {
                conf.Receive<IQuery<TestQuery>>((q, ctx) => q.Sender.Tell(new TestResponse { ID = q.Message.ID }, ctx.Self));
            });

            Sys.EventStream.Subscribe(responder, typeof(IQuery<TestQuery>));

            var query = new TestQuery();
            var response = await Sys.Query<TestResponse>(query, TimeSpan.FromSeconds(1));

            Assert.Equal(query.ID, response.ID);
        }


        [Fact]
        public async Task Query_using_actorpath_gets_response()
        {
            var responder = Sys.ActorOf(conf =>
            {
                conf.Receive<IQuery<TestQuery>>((q, ctx) => ctx.Sender.Tell(new TestResponse { ID = q.Message.ID }));
            });

            var path = responder.Path.ToString();

            

            var query = new TestQuery();
            var response = await Sys.Query<TestResponse>(ActorSelection(path), query, TimeSpan.FromSeconds(1));

            Assert.Equal(query.ID, response.ID);
        }

        [Fact]
        public async Task Query_using_eventstream_and_wrong_response_throws_exception()
        {
            var responder = Sys.ActorOf(conf =>
            {
                conf.Receive<IQuery<TestQuery>>((q, ctx) => ctx.Sender.Tell(new TestResponse { ID = q.Message.ID }));
            });

            Sys.EventStream.Subscribe(responder, typeof(IQuery<TestQuery>));

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await Sys.Query<string>(new TestQuery(), TimeSpan.FromSeconds(1));
            });
        }

        [Fact]
        public async Task Query_using_actorpath_and_wrong_response_throws_exception()
        {
            var responder = Sys.ActorOf(conf =>
            {
                conf.Receive<IQuery<TestQuery>>((q, ctx) => ctx.Sender.Tell(new TestResponse { ID = q.Message.ID }));
            });

            var path = responder.Path.ToString();

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await Sys.Query<string>(ActorSelection(path), new TestQuery(), TimeSpan.FromSeconds(1));
            });
        }

        [Fact]
        public async Task Query_using_eventstream_throws_exception_on_timeout()
        {
            await Assert.ThrowsAsync<QueryException>(async () =>
            {
                await Sys.Query<TestResponse>(new TestQuery(), TimeSpan.FromMilliseconds(10));
            });
        }

        [Fact]
        public async Task Query_using_actorpath_throws_exception_on_timeout()
        {
            await Assert.ThrowsAsync<QueryException>(async () =>
            {
                await Sys.Query<TestResponse>(new TestQuery(), TimeSpan.FromMilliseconds(10));
            });
        }
    }
}
