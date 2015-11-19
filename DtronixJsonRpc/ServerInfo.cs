using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ServerInfo {
		private string _Name;

		/// <summary>
		/// Name of the server.
		/// </summary>
		public string Name {
			get { return _Name; }
			set {
				if (value?.Length > 64) {
					throw new InvalidOperationException("Name must be under 64 characters.");
				}

				_Name = value;
			}
		}

		private string _Description;

		/// <summary>
		/// Description of the server.
		/// </summary>
		public string Description {
			get { return _Description; }
			set {
				if (value?.Length > 256) {
					throw new InvalidOperationException("Description must be under 256 characters.");
				}

				_Description = value;
			}
		}

		private string _Version;

		/// <summary>
		/// Version of the server.
		/// </summary>
		public string Version {
			get { return _Version; }
			set {
				if (value?.Length > 16) {
					throw new InvalidOperationException("Version must be under 16 characters.");
				}

				_Version = value;
			}
		}


	}
}
