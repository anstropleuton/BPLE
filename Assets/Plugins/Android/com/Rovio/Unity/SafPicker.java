package com.Rovio.Unity;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import com.unity3d.player.UnityPlayer;

public class SafPicker {
    private static final int REQUEST_CODE = 9917;

    private static volatile String pickedUri = "";

    private static volatile boolean pickerFinished = false;

    public static void openTreePicker(final Activity activity) {
        pickedUri = "";
        pickerFinished = false;
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
                intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
                activity.startActivityForResult(intent, REQUEST_CODE);
            }
        });
    }
    
    public static boolean isPickerFinished() {
        return pickerFinished;
    }
    
    public static String consumePickedUri() {
        String text = pickedUri;
        pickedUri = "";
        pickerFinished = false;
        return text;
    }

    public static void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode != REQUEST_CODE) {
            return;
        }
        try {
            if (resultCode == Activity.RESULT_OK && data != null) {
                Uri uri = data.getData();
                if (uri != null) {
                    int num = data.getFlags() & (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                    UnityPlayer.currentActivity.getContentResolver().takePersistableUriPermission(uri, num);
                    pickedUri = uri.toString();
                    pickerFinished = true;
                    return;
                }
            }
        } catch (Throwable throwable) {
            android.util.Log.w("SafPicker", "takePersistableUriPermission failed", throwable);
        }
        pickedUri = "";
        pickerFinished = true;
    }
}