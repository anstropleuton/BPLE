package com.Rovio.Unity;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.Settings;

import com.unity3d.player.UnityPlayerActivity;

public class PermissionGateActivity extends Activity {
    private boolean m_waitingForPermission;
    private boolean m_initialDialogShown;
    private boolean m_returnDialogShown;
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (HasAllFilesAccess()) {
            LaunchGame();
            return;
        }
        ShowInitialPermissionDialog();
    }
    @Override
    protected void onResume() {
        super.onResume();
        if (!m_waitingForPermission) {
            if (!HasAllFilesAccess() && !m_initialDialogShown) {
                ShowInitialPermissionDialog();
            }
            return;
        }
        m_waitingForPermission = false;
        if (HasAllFilesAccess()) {
            LaunchGame();
            return;
        }
        ShowDeniedPermissionDialog();
    }
    private boolean HasAllFilesAccess() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            return Environment.isExternalStorageManager();
        }
        return true;
    }
    private String GetAllowedFolderText() {
        String text = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS).getAbsolutePath();
        CharSequence charSequence = getApplicationInfo().loadLabel(getPackageManager());
        if (charSequence != null) {
            text = text + "/" + charSequence.toString();
        }
        return text;
    }
    private void ShowInitialPermissionDialog() {
        if (m_initialDialogShown) {
            return;
        }
        m_initialDialogShown = true;
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setTitle("Storage access needed!");
        builder.setMessage("BPLE now saves files at: " + GetAllowedFolderText() + ". Please grant full storage access from settings. Rest assured, only that path is managed.");
        builder.setCancelable(false);
        builder.setPositiveButton("Open settings", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                RequestPermission();
            }
        });
        builder.setNegativeButton("Exit", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                finish();
            }
        });
        builder.show();
    }
    private void ShowDeniedPermissionDialog() {
        if (m_returnDialogShown) {
            return;
        }
        m_returnDialogShown = true;
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setTitle("Permission still missing!");
        builder.setMessage("Storage access was not granted! Please grant access so BPLE can access and manage saves at: " + GetAllowedFolderText() + ". Rest assured, only that path is managed.");
        builder.setCancelable(false);
        builder.setPositiveButton("Try again", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                m_initialDialogShown = false;
                m_returnDialogShown = false;
                ShowInitialPermissionDialog();
            }
        });
        builder.setNegativeButton("Exit", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                finish();
            }
        });
        builder.show();
    }
    private void RequestPermission() {
        m_waitingForPermission = true;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            try {
                Intent intent = new Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION);
                startActivity(intent);
                return;
            } catch (Throwable throwable) {
                Intent intent = new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS);
                intent.setData(Uri.parse("package:" + getPackageName()));
                startActivity(intent);
                return;
            }
        }
        finish();
    }
    private void LaunchGame() {
        Intent intent = new Intent(this, UnityPlayerActivity.class);
        startActivity(intent);
        finish();
    }
}