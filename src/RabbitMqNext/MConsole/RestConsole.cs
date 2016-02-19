namespace RabbitMqNext.MConsole
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Text;
	using System.Threading.Tasks;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using Newtonsoft.Json;

	// https://cdn.rawgit.com/rabbitmq/rabbitmq-management/rabbitmq_v3_6_0/priv/www/api/index.html
	public class RestConsole : IDisposable
	{
		private readonly HttpClient _client;

		public RestConsole(string hostname, string username, string password)
		{
			_client = new HttpClient()
			{
				BaseAddress = new Uri(string.Format("http://{0}:15672/api/", hostname))
			};

			_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
				Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", username, password))));
			_client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("curl", "7.30.0"));
			_client.DefaultRequestHeaders.ConnectionClose = true;
		}

		// /api/whoami
		public async Task<UserInfo> GetCurrentUserTags()
		{
			// {"name":"guest","tags":"administrator"}

			var url = "whoami";
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<UserInfo>(json);
		}

		// /api/permissions
		public async Task<IEnumerable<UserVHostPermissionsInfo>> GetPermissions()
		{
			// {"user":"guest","vhost":"/", "configure":".*","write":".*","read":".*"} ]

			var url = "permissions";
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<UserVHostPermissionsInfo>>(json);
		}

		// /api/users
		public async Task<IEnumerable<UserInfo>> GetUsers()
		{
			// [ {"name":"clear","password_hash":"QsZFnQwc+7iURLd2SNz/kji6xkg=","tags":""},
			//   {"name":"guest","password_hash":"jACWlZf7UiEHZXhHdGcz8rrpXak=","tags":"administrator"} ]

			var url = "users";
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<UserInfo>>(json);
		}

		// /api/users/name
		public async Task AddUser(string name, string password, params string[] tags)
		{
			// PUT {"password":"secret","tags":"administrator"}

			var url = string.Format("users/{0}", WebUtility.UrlEncode(name));

			var payload = "{ \"password\": \"" + password + "\", \"tags\": \"" + string.Join(",", tags) + "\"  }";

			var response = await _client.PutAsync(url, new StringContent(payload, Encoding.UTF8, "application/json")).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
				throw new Exception(response.ReasonPhrase);
		}

		// /api/vhosts/name
		public async Task CreateVHost(string name)
		{
			// PUT /api/vhosts/name
			var url = string.Format("vhosts/{0}", WebUtility.UrlEncode(name));
			var response = await _client.PutAsync(url, new StringContent("{\"tracing\": false}", Encoding.UTF8, "application/json")).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
				throw new Exception(response.ReasonPhrase);
		}

		// /api/vhosts/name
		public async Task DeleteVHost(string name)
		{
			// DELETE /api/vhosts/name

			var url = string.Format("vhosts/{0}", WebUtility.UrlEncode(name));
			var response = await _client.DeleteAsync(url).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
				throw new Exception(response.ReasonPhrase);
		}

		// /api/vhosts/name/permissions
		public async Task<IEnumerable<UserVHostPermissionsInfo>> GetVHostPermissions(string name)
		{
			// [
			//  {"vhost":"clear_perf","user":"clear","configure":".*","write":".*","read":".*"},
			//  {"vhost":"clear_perf","user":"guest","configure":".*","write":".*","read":".*"}
			// ]

			var url = string.Format("vhosts/{0}/permissions", WebUtility.UrlEncode(name));

			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<UserVHostPermissionsInfo>>(json);
		}

		// /api/permissions/vhost/user
		public async Task SetUserVHostPermission(string name, string vhost, string configure = ".*", string write = ".*", string read = ".*")
		{
			// PUT /api/permissions/vhost/user
			// body {"configure":".*","write":".*","read":".*"}

			var url = string.Format("permissions/{0}/{1}", WebUtility.UrlEncode(vhost), WebUtility.UrlEncode(name));

			var payload = "{ \"configure\": \"" + configure + "\", \"write\": \"" + write + "\", \"read\": \"" + read + "\" }";

			var response = await _client.PutAsync(url, new StringContent(payload, Encoding.UTF8, "application/json")).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
				throw new Exception(response.ReasonPhrase);
		}

		// /api/bindings/vhost
		// A list of all bindings in a given virtual host.
		public async Task<IEnumerable<BindingInfo>> GetBindings(string vhost = "/")
		{
			var url = string.Format("bindings/{0}", WebUtility.UrlEncode(vhost));
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<BindingInfo>>(json);
		}

		// /api/bindings/vhost/e/exchange/q/queue
		// A list of all bindings between an exchange and a	queue. Remember, an	exchange and a queue can be	bound together many	times 
		public async Task<IEnumerable<BindingInfo>> GetBindings(string exchange, string queue, string vhost = "/")
		{
			var url = string.Format("bindings/{0}/e/{1}/q/{2}",
				WebUtility.UrlEncode(vhost),
				WebUtility.UrlEncode(exchange),
				WebUtility.UrlEncode(queue));
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<BindingInfo>>(json);
		}

		// /api/exchanges/vhost/exchange/bindings/source
		// A list of all bindings between an exchange and a	queue. Remember, an	exchange and a queue can be	bound together many	times 
		public async Task<IEnumerable<BindingInfo>> GetExchangeBindings(string exchange, string vhost = "/")
		{
			//var url =	string.Format("http://{0}:15672/api/exchanges/{2}/{1}/bindings/source",	config.Host	?? "localhost",	exchange, config.VHost ?? "%2F");
			var url = string.Format("exchanges/{0}/{1}/bindings/source",
				WebUtility.UrlEncode(vhost),
				WebUtility.UrlEncode(exchange));
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<BindingInfo>>(json);
		}

		// /api/exchanges/vhost
		public async Task<IEnumerable<ExchangeInfo>> GetExchanges(string vhost = "/")
		{
			var url = string.Format("exchanges/{0}", WebUtility.UrlEncode(vhost));
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<ExchangeInfo>>(json);
		}

		// /api/queues/vhost
		public async Task<IEnumerable<QueueInfo>> GetQueues(string vhost = "/")
		{
			var url = string.Format("queues/{0}", WebUtility.UrlEncode(vhost));
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<QueueInfo>>(json);
		}

		// /api/exchanges/vhost/name
		public async Task DeleteExchange(string name, string vhost)
		{
			var url = string.Format("exchanges/{0}/{1}", WebUtility.UrlEncode(vhost), WebUtility.UrlEncode(name));
			var response = await _client.DeleteAsync(url).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
				throw new Exception(response.ReasonPhrase);
		}

		public async Task DeleteQueue(string name, string vhost)
		{
			var url = string.Format("queues/{0}/{1}", WebUtility.UrlEncode(vhost), WebUtility.UrlEncode(name));
			var response = await _client.DeleteAsync(url).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
				throw new Exception(response.ReasonPhrase);
		}

		public async Task PurgeQueue(string name, string vhost)
		{
			var url = string.Format("queues/{0}/{1}/contents", WebUtility.UrlEncode(vhost), WebUtility.UrlEncode(name));
			var response = await _client.DeleteAsync(url).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
				throw new Exception(response.ReasonPhrase);
		}

		// /api/vhosts
		public async Task<IEnumerable<VHostInfo>> GetVHosts()
		{
			// "name": "clear_int_test", "tracing": false  - ignoring message_stats

			var url = "vhosts";
			var json = await _client.GetStringAsync(url).ConfigureAwait(false);

			return JsonConvert.DeserializeObject<IEnumerable<VHostInfo>>(json);
		}

		public void Dispose()
		{
			_client.Dispose();
		}
	}
}