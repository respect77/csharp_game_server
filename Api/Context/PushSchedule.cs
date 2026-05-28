using Server.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Api.Context
{
    public class PushSchedule
    {
        //private LogManager _logManager = LogManager.Instance;
        private MySqlModule _mysql;

        public PushSchedule(MySqlModule mysql)
        {
            _mysql = mysql;
            Schedule();
        }

        private async void Schedule()
        {
            
        }
    }
}
