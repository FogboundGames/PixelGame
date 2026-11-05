using System;
using System.Threading;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Google Play In-App Updates için ince JNI sarmalayıcı (kütüphane: Assets/Editor/LandFlowUpdateDependencies.xml).
    /// Play'in bu cihaza gerçekten yeni sürüm sunup sunmadığını söyler ve güncellemeyi Play'in kendi tam ekranıyla başlatır.
    /// Play'den kurulmamış cihazlarda (Waydroid, elle kurulan APK) sorgu başarısız olur; çağıran o zaman yalnızca Remote Config'e güvenir.
    /// </summary>
    public static class LandFlowPlayUpdate
    {
        public enum Availability
        {
            Unknown,      // Sorgu başarısız ya da Android değil
            NotAvailable,
            Available,
            InProgress,   // Daha önce başlatılmış tam ekran güncelleme yarıda kaldı
        }

        // com.google.android.play.core.install.model sabitleri
        private const int UpdateNotAvailable = 1;
        private const int UpdateAvailable = 2;
        private const int DeveloperTriggeredUpdateInProgress = 3;
        private const int AppUpdateTypeImmediate = 1;
        private const int RequestCode = 4721;

        /// <summary>Play'e bu cihaz için güncelleme olup olmadığını sorar. Sonuç ana iş parçacığında gelir.</summary>
        public static void Query(Action<Availability> onResult)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Request((info, manager) =>
            {
                if (info == null) return Availability.Unknown;
                switch (info.Call<int>("updateAvailability"))
                {
                    case UpdateAvailable: return Availability.Available;
                    case DeveloperTriggeredUpdateInProgress: return Availability.InProgress;
                    case UpdateNotAvailable: return Availability.NotAvailable;
                    default: return Availability.Unknown;
                }
            }, onResult);
#else
            onResult(Availability.Unknown);
#endif
        }

        /// <summary>Play'in tam ekran güncellemesini başlatır; başlatamazsa false döner (çağıran mağaza sayfasını açar).</summary>
        public static void StartImmediate(Action<bool> onStarted)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Request((info, manager) =>
            {
                if (info == null) return false;
                int availability = info.Call<int>("updateAvailability");
                if (availability != UpdateAvailable && availability != DeveloperTriggeredUpdateInProgress) return false;
                if (!info.Call<bool>("isUpdateTypeAllowed", AppUpdateTypeImmediate)) return false;
                using (AndroidJavaObject activity = CurrentActivity())
                {
                    return manager.Call<bool>("startUpdateFlowForResult", info, AppUpdateTypeImmediate, activity, RequestCode);
                }
            }, onStarted);
#else
            onStarted(false);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject s_Manager;

        private static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        /// <summary>AppUpdateInfo'yu ister, onu Java tarafında işler ve sonucu Unity'nin ana iş parçacığına taşır.</summary>
        private static void Request<T>(Func<AndroidJavaObject, AndroidJavaObject, T> handle, Action<T> onResult)
        {
            SynchronizationContext main = SynchronizationContext.Current;
            void Deliver(T value)
            {
                if (main != null) main.Post(_ => onResult(value), null);
                else onResult(value);
            }

            try
            {
                if (s_Manager == null)
                {
                    using (AndroidJavaObject activity = CurrentActivity())
                    using (var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory"))
                    {
                        s_Manager = factory.CallStatic<AndroidJavaObject>("create", activity);
                    }
                }

                AndroidJavaObject manager = s_Manager;
                AndroidJavaObject task = manager.Call<AndroidJavaObject>("getAppUpdateInfo");
                task.Call<AndroidJavaObject>("addOnSuccessListener", new SuccessListener(info =>
                {
                    T value;
                    try { value = handle(info, manager); }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[LandFlowPlayUpdate] {e.Message}");
                        value = handle(null, manager);
                    }
                    Deliver(value);
                }));
                task.Call<AndroidJavaObject>("addOnFailureListener", new FailureListener(error =>
                {
                    Debug.Log($"[LandFlowPlayUpdate] Play sorgusu başarısız: {error}");
                    Deliver(handle(null, manager));
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LandFlowPlayUpdate] Play güncelleme kütüphanesi kullanılamadı: {e.Message}");
                Deliver(handle(null, null));
            }
        }

        private sealed class SuccessListener : AndroidJavaProxy
        {
            private readonly Action<AndroidJavaObject> m_OnSuccess;

            public SuccessListener(Action<AndroidJavaObject> onSuccess) : base("com.google.android.gms.tasks.OnSuccessListener")
            {
                m_OnSuccess = onSuccess;
            }

            public void onSuccess(AndroidJavaObject result) => m_OnSuccess(result);
        }

        private sealed class FailureListener : AndroidJavaProxy
        {
            private readonly Action<string> m_OnFailure;

            public FailureListener(Action<string> onFailure) : base("com.google.android.gms.tasks.OnFailureListener")
            {
                m_OnFailure = onFailure;
            }

            public void onFailure(AndroidJavaObject exception) => m_OnFailure(exception != null ? exception.Call<string>("toString") : "unknown");
        }
#endif
    }
}
