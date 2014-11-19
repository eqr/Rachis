﻿// -----------------------------------------------------------------------
//  <copyright file="HttpTransportPingTest.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;
using Rhino.Raft.Messages;
using Rhino.Raft.Transport;
using Voron;
using Xunit;

namespace Rhino.Raft.Tests
{
	public class HttpTransportPingTest : IDisposable
	{
		private readonly IDisposable _server;
		private readonly RaftEngine _raftEngine;
		private readonly int _timeout = Debugger.IsAttached ? 50*1000 : 2500;
		private HttpTransport _node1Transport;

		public HttpTransportPingTest()
		{
			_node1Transport = new HttpTransport("node1");
			_node1Transport.Register(new NodeConnectionInfo { Name = "node1", Url = new Uri("http://localhost:9079") });
			_node1Transport.Register(new NodeConnectionInfo { Name = "node2", Url = new Uri("http://localhost:9078") });
			_node1Transport.Register(new NodeConnectionInfo { Name = "node3", Url = new Uri("http://localhost:9077") });

			var engineOptions = new RaftEngineOptions("node1", StorageEnvironmentOptions.CreateMemoryOnly(), _node1Transport, new DictionaryStateMachine())
			{
				AllVotingNodes = new[] { "node1", "node2", "node3" },
				MessageTimeout = 60*1000
			};
			_raftEngine = new RaftEngine(engineOptions);

			_server = WebApp.Start(new StartOptions
			{
				Urls = { "http://+:9079/" }
			}, builder =>
			{
				var httpConfiguration = new HttpConfiguration();
				RaftWebApiConfig.Register(httpConfiguration);
				httpConfiguration.Properties[typeof(HttpTransportBus)] = _node1Transport.Bus;
				builder.UseWebApi(httpConfiguration);
			});
		}

		[Fact]
		public void CanSendRequestVotesAndGetReply()
		{
			using (var node2Transport = new Transport.HttpTransport("node2"))
			{
				node2Transport.Register(new NodeConnectionInfo
				{
					Name = "node1",
					Url = new Uri("http://localhost:9079")
				});

				node2Transport.Send("node1", new RequestVoteRequest
				{
					TrialOnly = true,
					From = "node2",
					Term = 3,
					LastLogIndex = 2,
					LastLogTerm = 2,
				});

				MessageContext context;
				var gotIt = node2Transport.TryReceiveMessage(_timeout, CancellationToken.None, out context);

				Assert.True(gotIt);

				Assert.True(context.Message is RequestVoteResponse);
			}
		}


		[Fact]
		public void CanSendTimeoutNow()
		{
			using (var node2Transport = new Transport.HttpTransport("node2"))
			{
				node2Transport.Register(new NodeConnectionInfo
				{
					Name = "node1",
					Url = new Uri("http://localhost:9079")
				});

				node2Transport.Send("node1", new AppendEntriesRequest
				{
					From = "node2",
					Term = 2,
					PrevLogIndex = 0,
					PrevLogTerm = 0,
					LeaderCommit = 1,
					Entries = new[]
					{
						new LogEntry
						{
							Term = 2,
							Index = 1,
							Data = new JsonCommandSerializer().Serialize(new DictionaryCommand.Set
							{
								Key = "a",
								Value = 2
							})
						},
					}
				});
				MessageContext context;
				var gotIt = node2Transport.TryReceiveMessage(_timeout, CancellationToken.None, out context);

				Assert.True(gotIt);
				Assert.True(((AppendEntriesResponse) context.Message).Success);

				var mres = new ManualResetEventSlim();
				_raftEngine.StateChanged += state =>
				{
					if (state == RaftEngineState.CandidateByRequest)
						mres.Set();
				};

				node2Transport.Send("node1", new TimeoutNowRequest
				{
					Term = 4,
					From = "node2"
				});

				gotIt = node2Transport.TryReceiveMessage(_timeout, CancellationToken.None, out context);

				Assert.True(gotIt);

				Assert.True(context.Message is NothingToDo);

				Assert.True(mres.Wait(_timeout));
			}
		}

		[Fact]
		public void CanAskIfCanInstallSnapshot()
		{
			using (var node2Transport = new HttpTransport("node2"))
			{
				node2Transport.Register(new NodeConnectionInfo
				{
					Name = "node1",
					Url = new Uri("http://localhost:9079")
				});

				node2Transport.Send("node1", new CanInstallSnapshotRequest
				{
					From = "node2",
					Term = 2,
					Index = 3,
				});


				MessageContext context;
				var gotIt = node2Transport.TryReceiveMessage(_timeout, CancellationToken.None, out context);

				Assert.True(gotIt);
				var msg = (CanInstallSnapshotResponse) context.Message;
				Assert.True(msg.Success);
			}
		}

		[Fact]
		public void CanSendEntries()
		{
			using (var node2Transport = new HttpTransport("node2"))
			{
				node2Transport.Register(new NodeConnectionInfo
				{
					Name = "node1",
					Url = new Uri("http://localhost:9079")
				});

				node2Transport.Send("node1", new AppendEntriesRequest
				{
					From = "node2",
					Term = 2,
					PrevLogIndex = 0,
					PrevLogTerm = 0,
					LeaderCommit = 1,
					Entries = new LogEntry[]
					{
						new LogEntry
						{
							Term = 2,
							Index = 1,
							Data = new JsonCommandSerializer().Serialize(new DictionaryCommand.Set
							{
								Key = "a",
								Value = 2
							})
						},
					}
				});


				MessageContext context;
				var gotIt = node2Transport.TryReceiveMessage(_timeout, CancellationToken.None, out context);

				Assert.True(gotIt);

				var appendEntriesResponse = (AppendEntriesResponse) context.Message;
				Assert.True(appendEntriesResponse.Success);

				Assert.Equal(2, ((DictionaryStateMachine) _raftEngine.StateMachine).Data["a"]);
			}
		}

		[Fact]
		public void CanInstallSnapshot()
		{
			using (var node2Transport = new HttpTransport("node2"))
			{
				node2Transport.Register(new NodeConnectionInfo
				{
					Name = "node1",
					Url = new Uri("http://localhost:9079")
				});

				node2Transport.Send("node1", new CanInstallSnapshotRequest
				{
					From = "node2",
					Term = 2,
					Index = 3,
				});

				MessageContext context;
				var gotIt = node2Transport.TryReceiveMessage(_timeout, CancellationToken.None, out context);
				Assert.True(gotIt);
				Assert.True(context.Message is CanInstallSnapshotResponse);

				node2Transport.Stream("node1", new InstallSnapshotRequest
				{
					From = "node2",
					Term = 2,
					LastIncludedIndex = 2,
					LastIncludedTerm = 2,
				}, stream =>
				{
					var streamWriter = new StreamWriter(stream);
					var data = new Dictionary<string, int> {{"a", 2}};
					new JsonSerializer().Serialize(streamWriter, data);
					streamWriter.Flush();
				});


				gotIt = node2Transport.TryReceiveMessage(_timeout, CancellationToken.None, out context);

				Assert.True(gotIt);

				var appendEntriesResponse = (InstallSnapshotResponse) context.Message;
				Assert.True(appendEntriesResponse.Success);

				Assert.Equal(2, ((DictionaryStateMachine) _raftEngine.StateMachine).Data["a"]);
			}
		}

		public void Dispose()
		{
			_server.Dispose();
			_raftEngine.Dispose();
			_node1Transport.Dispose();

		}
	}
}