package com.Rovio.Unity;

import android.content.Intent;
import com.unity3d.player.UnityPlayerActivity;

public class CustomUnityPlayerActivity extends UnityPlayerActivity {
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        SafPicker.onActivityResult(
            requestCode,
            resultCode,
            data
        );
    }
}