import unittest
import threading
import urllib.request
import urllib.error
from http.server import ThreadingHTTPServer
from quota_service import weekly, Snapshot, handler_for, authorize_newapi


class QuotaTests(unittest.TestCase):
    def test_newapi_route_authorization(self):
        cfg = {'channelId': 1, 'cpaBaseUrl': 'http://cpa:8317'}
        row = {'status': 1, 'user_status': 1, 'restricted_ips': False,
               'model_limits_enabled': False, 'model_limits': '', 'group': 'default',
               'cross_group': False, 'routes': [{'id': 1, 'url': 'http://cpa:8317'}]}
        key = 'Bearer sk-' + 'A'*32
        def lookup(c, hashed, model):
            self.assertEqual(len(hashed), 64)
            self.assertNotIn('A'*32, hashed)
            return row
        check = lambda: authorize_newapi(cfg, key, 'gpt-6-astra', lookup)
        self.assertEqual(check(), 200)
        row['status'] = 2
        self.assertEqual(check(), 401)
        row['status'] = 1
        row['restricted_ips'] = True
        self.assertEqual(check(), 403)
        row['restricted_ips'] = False
        row['model_limits_enabled'], row['model_limits'] = True, 'gpt-5.6-sol'
        self.assertEqual(check(), 403)
        row['model_limits_enabled'] = False
        row['routes'].append({'id': 2, 'url': 'http://another:8317'})
        self.assertEqual(check(), 409)
        row['routes'] = []
        self.assertEqual(check(), 403)
        self.assertEqual(authorize_newapi(cfg, key, "model' SQL", lookup), 403)
        self.assertEqual(authorize_newapi(cfg, 'Bearer invalid', 'gpt-6-astra', lookup), 401)

    def test_newapi_http_checks_before_cache(self):
        calls = []
        snapshot = Snapshot(lambda: calls.append(1) or {'remainingPercent': 94})
        authorize = lambda cfg, key, model: 200 if key == 'Bearer fixture' and model == 'gpt-6-astra' else 401
        server = ThreadingHTTPServer(('127.0.0.1', 0), handler_for({}, snapshot, authorize))
        threading.Thread(target=server.serve_forever, daemon=True).start()
        url = 'http://127.0.0.1:' + str(server.server_port) + '/v1/quota/weekly'
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={'Authorization': 'Bearer fixture', 'X-Quota-Model': 'gpt-6-astra'})) as r:
                self.assertEqual(r.status, 200)
            with self.assertRaises(urllib.error.HTTPError) as caught:
                urllib.request.urlopen(url)
            self.assertEqual(caught.exception.code, 401)
            self.assertEqual(calls, [1])
        finally:
            server.shutdown()
            server.server_close()

    def test_weekly(self):
        p = {'rate_limit': {'primary_window': {'limit_window_seconds': 604800, 'used_percent': 5, 'reset_at': 1800000000}}}
        self.assertEqual(weekly(p, 0)['remainingPercent'], 95)
        self.assertEqual(set(weekly(p, 0)), {'remainingPercent', 'resetAt', 'queriedAt', 'windowSeconds'})

    def test_reject_missing_and_invalid(self):
        for p in [{}, {'rate_limit': {'secondary_window': {'limit_window_seconds': 2592000, 'used_percent': 5}}},
                  {'rate_limit': {'secondary_window': {'limit_window_seconds': 604800, 'used_percent': True}}}]:
            with self.assertRaises(ValueError):
                weekly(p, 0)

    def test_error_discards_success_and_hides_secret(self):
        s = Snapshot(lambda: {'remainingPercent': 95}, interval=0)
        self.assertTrue(s.get()[0])
        def fail():
            raise ValueError('private-secret')
        s.collector = fail
        self.assertEqual(s.get(), (False, {'error': 'upstream_quota_unavailable'}))

    def test_shared_cache(self):
        calls = []
        s = Snapshot(lambda: calls.append(1) or {}, interval=120)
        s.get()
        s.get()
        self.assertEqual(len(calls), 1)

    def test_http_auth_before_collection(self):
        calls = []
        snapshot = Snapshot(lambda: calls.append(1) or {'remainingPercent': 94})
        server = ThreadingHTTPServer(('127.0.0.1', 0), handler_for({'readToken': 'test-read-token'}, snapshot))
        threading.Thread(target=server.serve_forever, daemon=True).start()
        url = 'http://127.0.0.1:' + str(server.server_port) + '/quota/weekly'
        try:
            for headers in [{}, {'Authorization': 'Bearer wrong'}]:
                with self.assertRaises(urllib.error.HTTPError) as caught:
                    urllib.request.urlopen(urllib.request.Request(url, headers=headers))
                self.assertEqual(caught.exception.code, 401)
            self.assertEqual(calls, [])
            with urllib.request.urlopen(urllib.request.Request(url, headers={'Authorization': 'Bearer test-read-token'})) as r:
                self.assertEqual(r.status, 200)
                self.assertEqual(r.headers['Cache-Control'], 'no-store')
            self.assertEqual(calls, [1])
        finally:
            server.shutdown()
            server.server_close()


if __name__ == '__main__':
    unittest.main()
