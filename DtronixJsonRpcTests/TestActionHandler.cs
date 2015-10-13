using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	public class TestActionHandler : ActionHandler<TestActionHandler> {

		private TestClientActions _TestClientActions = null;
		public TestClientActions TestClientActions {
			get {
				return _TestClientActions ?? (_TestClientActions = new TestClientActions(Connector));
			}
		}

        private TestBenchmarkActions<TestActionHandler> _TestBenchmarkActions = null;
        public TestBenchmarkActions<TestActionHandler> TestBenchmarkActions {
            get {
                return _TestBenchmarkActions ?? (_TestBenchmarkActions = new TestBenchmarkActions<TestActionHandler>(Connector));
            }
        }

        

    }
}
