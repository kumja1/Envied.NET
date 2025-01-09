using System.Security.Cryptography;
using System.Text;

namespace Envied.Utils;

class EnviedHelper
{
   private static Type GetEnvValueType(string value)
   {
      if (bool.TryParse(value, out _))
      {
         return typeof(bool);
      }
      else if (int.TryParse(value, out _))
      {
         return typeof(int);
      }
      else if (long.TryParse(value, out _))
      {
         return typeof(long);
      }
      else if (float.TryParse(value, out _))
      {
         return typeof(float);
      }
      else if (double.TryParse(value, out _))
      {
         return typeof(double);
      }
      else if (DateTime.TryParse(value, out _))
      {
         return typeof(DateTime);
      }
      else
      {
         return typeof(string);
      }
   }

   public static object Decrypt(string encryptedValue, byte[] key)
   {
      var parts = encryptedValue.Split(':');
      var encrypted = Convert.FromBase64String(parts[1]);
      using Aes aes = Aes.Create();
      aes.Key = key;
      aes.IV = Convert.FromBase64String(parts[0]);
      using var decryptor = aes.CreateDecryptor();
      var decryptedValue = Encoding.UTF8.GetString(decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length));
      return Convert.ChangeType(decryptedValue, GetEnvValueType(decryptedValue));
   }
}