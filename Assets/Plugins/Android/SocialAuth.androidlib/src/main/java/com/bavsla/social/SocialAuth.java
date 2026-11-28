package com.bavsla.social;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executors;

/**
 * Trade of MTBS 3D: Android'de Google ile giriş (Credential Manager, "Google ile oturum aç" düğmesi akışı).
 * Kimlik jetonunun aud'u web istemcisidir (serverClientId); jeton Unity'de /tmtbs/auth/google'a gönderilir.
 * Unity tarafı Callback'i AndroidJavaProxy ile uygular.
 */
public final class SocialAuth {
    public interface Callback {
        void onResult(String provider, String token, String name, String error);
    }

    private SocialAuth() { }

    public static void google(final Activity activity, final String serverClientId, final Callback cb) {
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    GetSignInWithGoogleOption opt = new GetSignInWithGoogleOption.Builder(serverClientId).build();
                    GetCredentialRequest req = new GetCredentialRequest.Builder().addCredentialOption(opt).build();
                    CredentialManager cm = CredentialManager.create(activity);
                    cm.getCredentialAsync(activity, req, new CancellationSignal(), Executors.newSingleThreadExecutor(),
                        new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                            @Override public void onResult(GetCredentialResponse res) {
                                Credential c = res.getCredential();
                                if (c instanceof CustomCredential
                                        && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(c.getType())) {
                                    try {
                                        GoogleIdTokenCredential g = GoogleIdTokenCredential.createFrom(c.getData());
                                        String ad = g.getDisplayName();
                                        cb.onResult("google", g.getIdToken(), ad == null ? "" : ad, "");
                                    } catch (Exception e) {
                                        cb.onResult("google", "", "", String.valueOf(e.getMessage()));
                                    }
                                } else {
                                    cb.onResult("google", "", "", "Beklenmeyen kimlik türü");
                                }
                            }

                            @Override public void onError(GetCredentialException e) {
                                if (e instanceof GetCredentialCancellationException) cb.onResult("google", "", "", "iptal");
                                else cb.onResult("google", "", "", String.valueOf(e.getMessage()));
                            }
                        });
                } catch (Throwable t) {
                    cb.onResult("google", "", "", String.valueOf(t.getMessage()));
                }
            }
        });
    }
}
