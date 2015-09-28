using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
    [System.AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    sealed class ActionMethodAttribute : Attribute {
        // This is a positional argument
        public ActionMethodAttribute() {
        }

    }
}
