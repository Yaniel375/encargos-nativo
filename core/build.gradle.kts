plugins { id("org.jetbrains.kotlin.jvm") }
kotlin { jvmToolchain(17) }
sourceSets { named("test") { kotlin.srcDir("src/test/kotlin") } }
dependencies { testImplementation(kotlin("test")) }
// Las pruebas de conformidad se ejecutan con: gradle :core:vectors
tasks.register<JavaExec>("vectors") {
    group = "verification"; dependsOn("testClasses")
    classpath = sourceSets["test"].runtimeClasspath
    mainClass.set("com.agencia.encargos.core.Vectors")
    args = listOf("none")
}
// Ruta de clases del dispositivo de prueba Kotlin (la usan las pruebas cruzadas tests/interop_native.py)
tasks.register("testClasspath") {
    dependsOn("testClasses")
    doLast { layout.buildDirectory.file("cp.txt").get().asFile.writeText(sourceSets["test"].runtimeClasspath.asPath) }
}
