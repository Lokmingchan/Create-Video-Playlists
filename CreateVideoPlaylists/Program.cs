using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using System.Text;

namespace CreateVideoPlaylists
{
    class Program
    {
        private const string mpcpl_topLine = @"MPCPLAYLIST";
        private const string mpcpl_line1 = @"{0},type,0";
        private const string mpcpl_line2 = @"{0},filename,{1}";

        private const string vlc_top =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
    <playlist xmlns = ""http://xspf.org/ns/0/"" xmlns:vlc=""http://www.videolan.org/vlc/playlist/ns/0/"" version=""1"">
	    <title>Playlist</title>
	    <trackList>";
        private const string vlc_track =
@"          <track>
                <location>{0}</location>
          </track>";
        private const string vlc_bottom =
@"      </trackList>
    </playlist>";

        private const string videoFormats = @"mp4|flv|avi|mpg|mkv|wmv|mov|rm|rmvb|ogm";
        private const char delimiter = ';';
        private static int x;

        public static void Main(string[] args)
        {
            try
            {
                string localDbConnectionString = ConfigurationManager.ConnectionStrings["LocalDB"].ConnectionString;
                string strPlaylistQuery = @"SELECT * FROM PlaylistFolders";
                SqlConnection sqlConnection = new SqlConnection(localDbConnectionString);
                SqlCommand cmd = new SqlCommand(strPlaylistQuery, sqlConnection);
                SqlDataReader reader;
                cmd.CommandType = CommandType.Text;

                sqlConnection.Open();
                reader = cmd.ExecuteReader();
                
                while (reader.Read())
                {
                    //string source = reader["Source"].ToString();
                    string[] sources = reader["Source"].ToString().Split(delimiter);
                    string destination = reader["Destination"].ToString();
                    bool recurseFolders = (bool)reader["RecurseFoldersInd"];
                    bool isActive = (bool)reader["ActiveInd"];

                    string[] excludeFolderKeywords;
                    if (reader["ExcludeFolderKeywords"] != DBNull.Value) //If Keywords for exclude folder exists
                    {
                        string temp = reader["ExcludeFolderKeywords"].ToString();
                        excludeFolderKeywords = temp.Split(delimiter);
                    }
                    else
                    {
                        excludeFolderKeywords = null;
                    }
                        

                    if (isActive)
                    {
                        x = 0;
                        
                        AddToPlaylist(sources, destination, recurseFolders, excludeFolderKeywords);
                    }
                }

                sqlConnection.Close();
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }

        private static void AddToPlaylist(string[] sources, string destination, bool recurse, string[] keywords)
        {
            try
            {
                if (destination.EndsWith(".mpcpl"))
                {
                    StreamWriter file = new StreamWriter(destination, false);
                    file.WriteLine(mpcpl_topLine);
                    Console.WriteLine(mpcpl_topLine);
                    file.Close();

                    foreach (string source in sources)
                    {
                        AddToMpcplPlaylist(source, destination, recurse, keywords);
                    }
                }
                else if (destination.EndsWith(".xspf"))
                {
                    StreamWriter file = new StreamWriter(destination, false);
                    file.WriteLine(vlc_top);
                    Console.WriteLine(vlc_top);
                    file.Close();

                    foreach (string source in sources)
                    {
                        AddToVlcPlaylist(source, destination, recurse, keywords);
                    }

                    file = new StreamWriter(destination, true);
                    file.WriteLine(vlc_bottom);
                    Console.WriteLine(vlc_bottom);
                    file.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void AddToMpcplPlaylist(string source, string destination, bool recurse, string[] keywords)
        {
            try
            {
                StreamWriter file = new StreamWriter(destination, true);
                foreach (string video in Directory.EnumerateFiles(source))
                {
                    if (IsVideo(video))
                    {
                        x++;
                        file.WriteLine(string.Format(mpcpl_line1, x));
                        Console.WriteLine(string.Format(mpcpl_line1, x));
                        file.WriteLine(string.Format(mpcpl_line2, x, video));
                        Console.WriteLine(string.Format(mpcpl_line2, x, video));
                    }
                }
                file.Close();

                if (recurse)
                {
                    foreach (string dir in Directory.EnumerateDirectories(source))
                    {
                        if (keywords == null || !excludeFolder(dir, keywords)) //if keywords is not null and dir not in keywords
                        {
                            AddToMpcplPlaylist(dir, destination, recurse, keywords);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void AddToVlcPlaylist(string source, string destination, bool recurse, string[] keywords)
        {
            try
            {
                StreamWriter file = new StreamWriter(destination, true);
                foreach (string video in Directory.EnumerateFiles(source))
                {
                    if (IsVideo(video))
                    {
                        string temp_track = string.Format(vlc_track, FilePathToFileUrl(video));
                        file.WriteLine(temp_track);
                        Console.WriteLine(temp_track);
                    }
                }
                file.Close();

                if (recurse)
                {
                    foreach (string dir in Directory.EnumerateDirectories(source))
                    {
                        if (keywords == null || !excludeFolder(dir, keywords)) //if keywords is not null and dir not in keywords
                        {
                            AddToVlcPlaylist(dir, destination, recurse, keywords);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static bool IsVideo(string videoFile)
        {
            try
            {
                string[] videoFormatArray = videoFormats.Split('|');

                foreach (string s in videoFormatArray)
                {
                    if (videoFile.EndsWith(s))
                        return true;
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }

            return false;
        }

        private static bool excludeFolder(string dir, string[] keywords)
        {
            bool excludeDir = false;
            foreach (string keyword in keywords)
            {
                if (dir.Contains(keyword))
                {
                    excludeDir = true;
                    break;
                }
            }

            return excludeDir;
        }

        public static string FilePathToFileUrl(string filePath)
        {
            StringBuilder uri = new StringBuilder();
            foreach (char v in filePath)
            {
                if ((v >= 'a' && v <= 'z') || (v >= 'A' && v <= 'Z') || (v >= '0' && v <= '9') ||
                  v == '+' || v == '/' || v == ':' || v == '.' || v == '-' || v == '_' || v == '~' ||
                  v > '\xFF')
                {
                    uri.Append(v);
                }
                else if (v == Path.DirectorySeparatorChar || v == Path.AltDirectorySeparatorChar)
                {
                    uri.Append('/');
                }
                else
                {
                    uri.Append(String.Format("%{0:X2}", (int)v));
                }
            }
            if (uri.Length >= 2 && uri[0] == '/' && uri[1] == '/') // UNC path
                uri.Insert(0, "file:");
            else
                uri.Insert(0, "file:///");
            return uri.ToString();
        }
    }
}
