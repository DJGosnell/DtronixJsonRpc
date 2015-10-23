﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class JsonRpcActionArgs : INotifyPropertyChanged {
		public JsonRpcSource Source { get; set; } = JsonRpcSource.Unset;

		public event PropertyChangedEventHandler PropertyChanged;

		protected void RequireStringLength(string str, int min, int max, bool allow_null, [System.Runtime.CompilerServices.CallerMemberName] string member_name = "") {
			if (str.Length < min) {
				throw new InvalidOperationException($"{member_name} must be more than {min} characters.");
			} else if (str.Length > max) {
				throw new InvalidOperationException($"{member_name} must be less than {max} characters.");
			} else if (allow_null == false && str == null) {
				throw new InvalidOperationException($"{member_name} must not be null.");
			}
		}

		protected bool SetField<T>(ref T field, T value, [CallerMemberName] string property_name = null) {
			if (EqualityComparer<T>.Default.Equals(field, value)) {
				return false;
			}
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property_name));
			return true;
		}
	}
}
