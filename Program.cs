using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using NAudio.Wave.Compression;
using NLayer;
using NAudio.Wave;
using System.IO;

namespace ConsoleApp1DNS
{

    public class Client
    {
        public UdpClient udp;
        public IPEndPoint hostEndPoint;
        public string hostname;
        public int nport;

        

        public Client(string hostName, int port)
        {
            hostEndPoint = new IPEndPoint(IPAddress.Parse(hostName), port);
            udp = new UdpClient(hostName, port);
            hostname = hostName;
            nport = port;
        }

        public byte[] Send(byte[] bytes)
        {
            udp.Send(bytes, bytes.Length);
            return udp.Receive(ref hostEndPoint);
        }
    };


    class Program
    {

        static public int times = 0;
        static public UInt64 bytes_send = 0;
        static public UInt64 memcnt = 0;
        static public bool connection_ok = false;

        static public Client ct;

        static void Main(string[] args)
        {

            // adres i port mikrokontrolera w sieci lokalnej  
            ct = new Client("192.168.1.20", 52001);

            while (true)
            {

                // w tym folderze znajduja sie pliki z muzyka
                string folder_search = @"I:\Różne";

                List<KeyValuePair<int, string>> dircnt = new List<KeyValuePair<int, string>>();


                // pliki licze od 1, 0 nie jest numerem pliku,
                int files_cnt = 1;
                int fileKey = 0;


                // wczytuje pliki .mp3 z lokalizacji i nadaje im numer
                foreach (string file in Directory.EnumerateFiles(folder_search, "*.mp3"))
                {
                    dircnt.Add(new KeyValuePair<int, string>(files_cnt++, file));
                }


                // prezentuje pliki mozliwe do odtworzenia, po 20 na raz, az zostanie wybrana wartosc wieksza niz 
                // zero i nie wieksza niz liczba utworow w katalogu 
                do
                {
                    int writeFileCnt = 0;

                    Console.WriteLine("Type file number to play");

                    foreach (var r in dircnt)
                    {
                        Console.WriteLine(r.Key + " " + r.Value);
                        writeFileCnt++;

                        if ((writeFileCnt % 20) == 0)
                        {
                            string s = Console.ReadLine();
                            if (Int32.TryParse(s, out fileKey))
                            {
                                break;
                            }
                        };
                    }
                } while (fileKey == 0);


                // wyswietlam nazwe wybranego pliku 
                string fileToPlay = "";

                foreach (var r in dircnt)
                {
                    if (r.Key == fileKey)
                    {
                        fileToPlay = r.Value;
                        break;
                    }
                }

                Console.WriteLine(fileToPlay);
                


                // w podanej lokalizacji tworze plik .wav, zawierajacy wartosci probek dzwieku
                // te dane sa wysylane w swiat
                var outfile = @"C:\naudio_test\test_wav.wav";

                using (var reader = new Mp3FileReader(fileToPlay))
                {
                    WaveFileWriter.CreateWaveFile(outfile, reader);
                }

                byte[] test_array = File.ReadAllBytes(outfile);

                // zasadniczo samplerate = 44100
                int SampleRate = (int)test_array[25] * 256 + (int)test_array[24];


                if(SampleRate != 44100)
                {
                    Console.WriteLine("wrong sample rate : " + SampleRate);
                    Console.ReadKey();
                    System.Environment.Exit(0);
                }

                int test_array_offset = test_array.Length;
                int pack_cnt = test_array_offset / 1000;

                int test_offset = 0;

                times = 0;

                Console.WriteLine(test_array.Length);
                Console.WriteLine("press something");
                Console.ReadKey();

                bool blok = true;


                
                //// wysyla dane testujace polaczenie az okaze sie, ze te jest stabilne
                //do
                //{
                //    byte[] st = new byte[10];
                    
                //    for (int i = 0; i < 10; i++)
                //    {
                //        st[i] = 1;
                //    }

                //    ct.Send(st);

                //    Thread.Sleep(10);

                //    var sr = ct.udp.Receive(ref ct.hostEndPoint);

                //    if (sr[0] == 0xAA)
                //    {
                //        blok = false;
                //    }

                //} while (blok);

                //Console.WriteLine("połączenie odnowione");


                // tu zaczyna sie odtwarzanie, czyli wysylanie paczek z dzwiekiem do uC, z uC do kodeka i z kodeka do glosnikow
                while ((test_offset + 1044) < test_array.Length)
                {

                    byte[] ps = new byte[1001];
                    ps[1000] = 0;

                    for (int n = 0; n < 1000; n++)
                    {
                        ps[n] = test_array[test_offset + 44];
                        test_offset++;
                    }

                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        switch (key.Key)
                        {

                            // przewija do przodu
                            case ConsoleKey.M:
                                {
                                    int dx = (int)Math.Truncate( (double) (test_array.Length / 100));
                                    if( (dx % 2) != 0)
                                    {
                                        dx += 1;
                                    }

                                    if (test_offset > pack_cnt + dx)
                                    {
                                        test_offset += dx;
                                    };
                                    break;
                                }

                            // przewija do tylu
                            case ConsoleKey.Z:
                                {
                                    int dx = (int)Math.Truncate((double)(test_array.Length / 100));
                                    if ((dx % 2) != 0)
                                    {
                                        dx += 1;
                                    }
                                    if (test_offset > dx)
                                    {
                                        test_offset -= dx;
                                    };
                                    break;
                                }

                            // pausa az do ponownego wcisniecia czekolowiek
                            case ConsoleKey.H:

                                byte[] pps = new byte[1001];

                                for (int n = 0; n < 1001; n++)
                                {
                                    pps[n] = 0;
                                }

                                for (int q = 0; q < 2; q++)
                                {
                                    var rr = ct.Send(pps);
                                }
                                Console.ReadKey();

                                break;

                            //konczy prace, ale zanim to zrobi informuje uC o zatrzymaniu odtwarzania oraz czysci bufor nadawania do kodeka
                            case ConsoleKey.Escape:
                                {
                                    for (int i = 0; i < 10; i++)
                                    {
                                        byte[] esc = new byte[1001];

                                        for (int n = 0; n < 1001; n++)
                                        {
                                            esc[n] = 0;
                                        }


                                        var resc = ct.Send(ps);
                                    }

                                    byte[] escpend = new byte[1001];

                                    for (int n = 0; n < 1000; n++)
                                    {
                                        escpend[n] = 0;
                                    }

                                    escpend[1000] = 3;

                                    var rndesc = ct.Send(escpend);
                                    System.Environment.Exit(0);
                                    break;
                                }

                            // wysyla w bajcie kontrolnym info, zeby kodek gral glosniej
                            case ConsoleKey.V:
                                {
                                    ps[1000] = 1;
                                    break;
                                }

                            // kodek ma wejscia analogowe tu mozna je podglosnic
                            case ConsoleKey.A:
                                {
                                    ps[1000] = 4;
                                    break;
                                }

                            // kodek ma wejscia analogowe tu mozna je sciszyc
                            case ConsoleKey.Q:
                                {
                                    ps[1000] = 5;
                                    break;
                                }

                            // wysyla w bajcie kontrolnym info, zeby kodek gral ciszej
                            case ConsoleKey.L:
                                {
                                    ps[1000] = 2;
                                    break;
                                }
                            default: break;
                        }
                    }

                    // wysyla paczke danych i czeka na odpowiedz z uC
                    var r = ct.Send(ps);

                    //if (r[3] > 0)
                    //{
                    //    Console.WriteLine("XDMAC error " + r[0] + " " + r[1] + " " + r[2] + " " + r[3]);
                    //    //Console.ReadKey();
                    //}

                    double pr = 100.0 * (double)(test_offset) / (double)test_array_offset;

                    //pokazuje ile % pliku juz zostalo odtworzonych
                    Console.WriteLine(test_offset + " B of " + test_array_offset + " B = " + Math.Round(pr, 2) + " %");
                }


                // na koniec utworu czysci bufor nadajnika do kodeka
                for (int i = 0; i < 10; i++)
                {
                    byte[] ps = new byte[1001];

                    for (int n = 0; n < 1000; n++)
                    {
                        ps[n] = 0;
                    }

                    
                    var r = ct.Send(ps);
                }

                // na koniec wylacza rowniez dma w uC - uC moze isc spac
                byte[] pend = new byte[1001];

                for (int n = 0; n < 1000; n++)
                {
                    pend[n] = 0;
                }

                pend[1000] = 3;

                var rnd = ct.Send(pend);

            }
        }
    }
}
