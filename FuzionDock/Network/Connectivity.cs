using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Network
{
    class Connectivity
    {
        public static bool HasNetworkConnection(string URL)
        {
            try
            {
                Uri uri = new Uri(URL);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.Timeout = 5000;
                request.Credentials = CredentialCache.DefaultNetworkCredentials;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
