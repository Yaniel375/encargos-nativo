plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
    id("com.google.devtools.ksp")
}

// Firma: el almacén de claves NUNCA va en el repositorio. En GitHub se pasa como secreto (ver LEEME.md).
val ksFile = System.getenv("ENCARGOS_KEYSTORE")?.let { file(it) }

android {
    namespace = "com.agencia.encargos.nativo"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.agencia.encargos.nativo"
        minSdk = 26
        targetSdk = 34
        // En GitHub Actions cada compilación sube el número para que la app se pueda actualizar encima.
        val run = (System.getenv("GITHUB_RUN_NUMBER") ?: "0").toIntOrNull() ?: 0
        versionCode = 100 + run
        versionName = "1.0.$run"
        vectorDrawables { useSupportLibrary = true }
        // Solo la copia de PRUEBA automática (nunca la que se publica) permite capturas de pantalla.
        buildConfigField("boolean", "CAPTURE_TEST", if (System.getenv("ENCARGOS_CAPTURE_TEST") == "1") "true" else "false")
    }

    signingConfigs {
        if (ksFile != null && ksFile.exists()) {
            create("release") {
                storeFile = ksFile
                storePassword = System.getenv("ENCARGOS_KEYSTORE_PASSWORD")
                keyAlias = System.getenv("ENCARGOS_KEY_ALIAS") ?: "encargos"
                keyPassword = System.getenv("ENCARGOS_KEY_PASSWORD") ?: System.getenv("ENCARGOS_KEYSTORE_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = if (ksFile != null && ksFile.exists()) signingConfigs.getByName("release") else signingConfigs.getByName("debug")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    buildFeatures { compose = true; buildConfig = true }
    packaging { resources { excludes += "/META-INF/{AL2.0,LGPL2.1}" } }
    // lintVitalRelease marca targetSdk 34 como "expirado" según la fecha de Play Store y frenaría el APK de CI.
    lint { disable += "ExpiredTargetSdkVersion" }
}

ksp { arg("room.schemaLocation", "$projectDir/schemas") }

dependencies {
    implementation(project(":core"))

    val composeBom = platform("androidx.compose:compose-bom:2024.09.03")
    implementation(composeBom)
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-graphics")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    debugImplementation("androidx.compose.ui:ui-tooling")
    implementation("androidx.compose.ui:ui-tooling-preview")

    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.activity:activity-compose:1.9.2")
    implementation("androidx.fragment:fragment-ktx:1.8.4")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.6")
    implementation("androidx.lifecycle:lifecycle-process:2.8.6")

    val room = "2.6.1"
    implementation("androidx.room:room-runtime:$room")
    implementation("androidx.room:room-ktx:$room")
    ksp("androidx.room:room-compiler:$room")

    implementation("androidx.work:work-runtime-ktx:2.9.1")
    implementation("androidx.biometric:biometric:1.1.0")
    implementation("androidx.print:print:1.0.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")
}
