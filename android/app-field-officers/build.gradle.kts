plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.compose)
    alias(libs.plugins.ksp)
    alias(libs.plugins.hilt)
}

android {
    namespace = "org.ffp.mobile.fieldofficers"
    compileSdk = 37

    defaultConfig {
        minSdk = 26
        applicationId = "org.ffp.mobile.fieldofficers"
        targetSdk = 37
        versionCode = 1
        versionName = "0.1.0"
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    buildFeatures { compose = true }
}

dependencies {
    implementation(project(":feature-field"))
    implementation(project(":core-security"))
    implementation(project(":core-network"))
    implementation(libs.activity)
    implementation(libs.hilt)
    ksp(libs.hilt.compiler)
}
