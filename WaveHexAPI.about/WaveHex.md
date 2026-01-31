1, after you create your executor, make sure its debug and x64 (any cpu -> x64 basicly)
2, open files, your x64 debug folder and paste there WaveModule with Injector
3, Codes for Execute and Injector are listed here:
// -----Injector:-----
string InjectorPath = System.IO.Path.Combine(Application.StartupPath, "Injector.exe");

System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(InjectorPath) { UseShellExecute = true });

// -----Execute:-----
try
{
using (TcpClient Client = new TcpClient("127.0.0.1", 6969))
using (NetworkStream Stream = Client.GetStream())
{
byte[] ScriptBytes = Encoding.UTF8.GetBytes(richTextBox1.Text);
int ScriptLength = ScriptBytes.Length;

                    byte[] LengthBytes = BitConverter.GetBytes(ScriptLength);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(LengthBytes);

                    Stream.Write(LengthBytes, 0, 4);
                    Stream.Write(ScriptBytes, 0, ScriptBytes.Length);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to Execute script: " + ex.Message, "WaveHax", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

4, Make sure to add those codes into your buttons (dont forget to change richTextBox1.Text to your monaco if you have one.)system
