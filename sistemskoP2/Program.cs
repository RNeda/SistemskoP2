using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace P2
{
    class Program
    {
        static readonly string root = @"C:\Users\Korisnik\Desktop\faks prez\sistemsko\sistemskoP2";
        static readonly IDictionary<string, string> kes = new Dictionary<string, string>();
        static readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();

       
        static void Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5050/");
            listener.Start();

            Console.WriteLine("Listening...");

            while (true)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    Task task = Task.Run(() => ObradiZahtevAsync(context));
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.Message);
                }
            }
        }

        static async Task ObradiZahtevAsync(object state)
        {
            HttpListenerContext context = (HttpListenerContext)state;

            string url = context.Request.Url!.LocalPath;
            string[] trazeneReci = url.Split('&', StringSplitOptions.RemoveEmptyEntries);
            string pom = trazeneReci[0];
            string pom2 = pom.Remove(0, 1);//uklanja / sa pocetka prve trazene reci
            trazeneReci[0] = pom2;
            string cacheKey = string.Join("&", trazeneReci);
            string htmltext = "";

            // provera da li je pretraga za trazenu rec vec u kesu
            cacheLock.EnterReadLock();
            if (kes.TryGetValue(cacheKey, out string? htmltextresponse))
            {
                string[] reci = htmltextresponse.Split("|");

                Console.WriteLine("Pronadjeno u kesu: \n");

                for (int i = 1; i < reci.Count(); i += 2)
                {
                    Console.WriteLine(reci[i]);
                }

                //vraca klijentu odgovor
                byte[] bafer = System.Text.Encoding.UTF8.GetBytes(htmltextresponse);
                context.Response.ContentLength64 = bafer.Length;
                await context.Response.OutputStream.WriteAsync(bafer, 0, bafer.Length);
                context.Response.OutputStream.Close();

                cacheLock.ExitReadLock();

            }
            else
            {
                //ako nije u kesu, dodajemo u kes
                cacheLock.ExitReadLock();

                htmltext = await VratiHtmlAsync(trazeneReci);
                cacheLock.EnterWriteLock();
                kes[cacheKey] = htmltext;
                cacheLock.ExitWriteLock();

                //vraca klijentu odgovor
                byte[] bafer = System.Text.Encoding.UTF8.GetBytes(htmltext);
                context.Response.ContentLength64 = bafer.Length;
                await context.Response.OutputStream.WriteAsync(bafer, 0, bafer.Length);
                context.Response.OutputStream.Close();
            }



            Console.WriteLine($"Zahtev {url} uspesno obradjen\n\n");
            Console.WriteLine("************************************");
        }

        static async Task<string> VratiHtmlAsync(string[] reci)
        {
            string htmlOdgovor = "<html><body><h3>Fajlovi koji ispunjavaju zahtev: </h3>";

            Console.WriteLine("root direktorijum: ");
            Console.WriteLine(root);

            foreach (string rec in reci)
            {
                Console.WriteLine("trazena rec: " + rec);

                List<Fajl> fajlovi = await PronadjiReci(rec, root);

                foreach (Fajl fajl in fajlovi)
                {
                    htmlOdgovor += $"<p>|Rec { rec } se pojavljuje u fajlu {fajl.imeFajla} { fajl.brPojavljivanja} puta|</p>";
                }

                htmlOdgovor += "</body></html>";
            }

            var sb = new StringBuilder();
            sb.Append(htmlOdgovor);
            return sb.ToString();
        }
        
        static async Task<List<Fajl>> PronadjiReci(string rec, string root)
        {
            List<Fajl> nadjenifajlovi = new List<Fajl>();
            string rootDir = @root;
            string trazirec = rec;

            foreach (string filePath in Directory.EnumerateFiles(rootDir, "*.txt", SearchOption.TopDirectoryOnly))
            {

                int count = NadjiUFajlu(filePath, trazirec).Result;
                Fajl trenutniFajl = new Fajl();

                if (count>0) 
                {
                    Console.WriteLine($"Rec {trazirec} se pojavljuje u fajlu {Path.GetFileName(filePath)} {count} puta");

                    trenutniFajl.imeFajla = Path.GetFileName(filePath);
                    trenutniFajl.brPojavljivanja = count;
                    nadjenifajlovi.Add(trenutniFajl);
                }

            }
            return nadjenifajlovi;
        }

        static async Task<int> NadjiUFajlu(string filePath, string trazirec)
        {
            int count = 0;
            
            string tekstfajla =await File.ReadAllTextAsync(filePath); 

            //pronalazi podudaranja trazene reci u fajlu i vraca broj podudaranja
            count = Regex.Matches(tekstfajla, trazirec).Count;
            
            return count;
            
        }

        public class Fajl
        {
            public int brPojavljivanja;
            public string imeFajla = "";
        }


    }
}
