using System;
using System.Collections.Generic;
using System.Text;

namespace ACACommon
{
    public static class ACAUtils
    {
        public class ChatMessage
        {
            public string Channel;
            public int ID;
            public string PlayerName;
            public string Mode;
            public string Content;

            public const string GlobalChannel = "Global";
        }

        public static ChatMessage InterpretChatMessage(string ln)
        {
            try
            {
                // filter out emote and quest spam
                if (!ln.StartsWith("[") && !ln.StartsWith("<"))
                    return null;

                int i;

                string channel = "Global";
                if (ln.StartsWith("["))
                {
                    ln = ln.Substring(1);
                    i = ln.IndexOf(']');

                    channel = ln.Substring(0, i);

                    ln = ln.Substring(i + 1/*end bracket*/ + 1/*space*/);
                }

                const string prefix = "<Tell:IIDString:";
                if (!ln.StartsWith(prefix))
                    return null;
                
                ln = ln.Substring(prefix.Length);
                i = ln.IndexOf(':');

                string sID = ln.Substring(0, i);
                ln = ln.Substring(i + 1);

                int id = int.Parse(sID);

                i = ln.IndexOf('>');
                string playerName = ln.Substring(0, i);
                ln = ln.Substring(i + 1);


                // skip rest of garbage


                const string endTag = @"<\Tell>";
                i = ln.IndexOf(endTag);
                ln = ln.Substring(i + endTag.Length);


                ln = ln.Substring(1);// skip extra space
                i = ln.IndexOf(',');
                string mode = ln.Substring(0, i);
                ln = ln.Substring(i + 1/*comma*/ + 1/*space*/ + 1/*openquote*/);

                string content = ln.Substring(0, ln.Length - 1/*closequote*/ - 1/*uhh donno.. newline?*/);



                ChatMessage cm = new ChatMessage();

                cm.Channel = channel;
                cm.ID = id;
                cm.PlayerName = playerName;
                cm.Mode = mode;
                cm.Content = content;

                return cm;
            }
            catch
            {
                return null;
            }
        }
    }
}
