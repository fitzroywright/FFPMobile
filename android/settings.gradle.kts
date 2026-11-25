pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "FFPMobile"
include(
    ":app-field-officers", ":app-warehouse", ":app-security-post",
    ":core-ui", ":core-auth", ":core-database", ":core-network", ":core-sync", ":core-security",
    ":feature-field", ":feature-warehouse", ":feature-security",
)
