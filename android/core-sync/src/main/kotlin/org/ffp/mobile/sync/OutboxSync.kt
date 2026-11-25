package org.ffp.mobile.sync

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import dagger.Module
import dagger.Provides
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.ffp.mobile.database.MobileDatabase
import org.ffp.mobile.database.OutboxEntry
import org.ffp.mobile.database.TransactionStage
import org.ffp.mobile.security.KeystoreVault
import java.io.IOException
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

data class SyncScope(val applicationId: String, val accountId: String)

data class ServerReceipt(
    val transactionId: String,
    val revision: Long,
    val stage: TransactionStage,
    val issue: String? = null,
)

interface OutboxTransport {
    /** The server must replay the original receipt when the same key is submitted again. */
    suspend fun submit(entry: OutboxEntry, idempotencyKey: String): ServerReceipt
    suspend fun reconcile(entry: OutboxEntry): ServerReceipt
}

interface SyncSessionProvider {
    /** Return the same instance for the life of the current authenticated session. */
    fun activeSession(): SyncSession?
}

data class SyncSession(val scope: SyncScope, val transport: OutboxTransport)

internal fun ServerReceipt.canApplyTo(entry: OutboxEntry): Boolean =
    transactionId == entry.id && revision > entry.serverRevision && entry.stage.permits(stage)

class OutboxRepository @Inject constructor(
    private val database: dagger.Lazy<MobileDatabase>,
    private val scheduler: OutboxScheduler,
    private val sessions: SyncSessionProvider,
) {
    suspend fun enqueue(entry: OutboxEntry) = withContext(Dispatchers.IO) {
        require(entry.stage == TransactionStage.LOCALLY_SAVED && entry.serverRevision == 0L && entry.issue == null)
        val scope = checkNotNull(sessions.activeSession()).scope
        require(scope == SyncScope(entry.applicationId, entry.accountId))
        scheduler.scheduleRecovery()
        database.get().outbox().insert(entry)
        scheduler.requestSync()
    }
}

class OutboxScheduler @Inject constructor(@ApplicationContext private val context: Context) {
    private val connected = Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()

    fun requestSync() {
        WorkManager.getInstance(context).enqueueUniqueWork(
            "ffp-outbox", ExistingWorkPolicy.KEEP,
            OneTimeWorkRequestBuilder<OutboxWorker>()
                .setConstraints(connected)
                .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 30, TimeUnit.SECONDS)
                .build(),
        )
    }

    internal suspend fun scheduleRecovery() = withContext(Dispatchers.IO) {
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(
            "ffp-outbox-recovery", ExistingPeriodicWorkPolicy.KEEP,
            PeriodicWorkRequestBuilder<OutboxRecoveryWorker>(15, TimeUnit.MINUTES)
                .setConstraints(connected)
                .build(),
        ).result.get()
    }
}

class OutboxRecoveryWorker(context: Context, parameters: WorkerParameters) :
    CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        OutboxScheduler(applicationContext).requestSync()
        return Result.success()
    }
}

class OutboxWorker(context: Context, parameters: WorkerParameters) :
    CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        val graph = EntryPointAccessors.fromApplication(applicationContext, OutboxEntryPoint::class.java)
        val session = graph.sessions().activeSession() ?: return@withContext Result.success()
        val dao = graph.database().outbox()
        val scope = session.scope
        var afterTime = Long.MIN_VALUE
        var afterId = ""
        var retry = false
        do {
            val batch = dao.pending(scope.applicationId, scope.accountId, afterTime, afterId, BATCH_SIZE)
            for (entry in batch) {
                if (graph.sessions().activeSession() !== session) return@withContext Result.success()
                try {
                    val receipt = if (entry.stage == TransactionStage.LOCALLY_SAVED) {
                        session.transport.submit(entry, idempotencyKey = entry.id)
                    } else {
                        session.transport.reconcile(entry)
                    }
                    if (receipt.canApplyTo(entry)) {
                        dao.reconcile(entry.id, entry.serverRevision, receipt.stage, receipt.revision, receipt.issue)
                    }
                } catch (_: IOException) {
                    retry = true
                }
                afterTime = entry.createdAtMillis
                afterId = entry.id
            }
        } while (batch.size == BATCH_SIZE)
        if (retry) Result.retry() else Result.success()
    }

    private companion object {
        const val BATCH_SIZE = 100
    }
}

@EntryPoint
@InstallIn(SingletonComponent::class)
interface OutboxEntryPoint {
    fun database(): MobileDatabase
    fun sessions(): SyncSessionProvider
}

@Module
@InstallIn(SingletonComponent::class)
object SyncModule {
    @Provides
    @Singleton
    fun database(@ApplicationContext context: Context, vault: KeystoreVault): MobileDatabase =
        MobileDatabase.open(context, vault)

    // Production transport requires the versioned transaction API and commissioned enrollment.
    @Provides
    fun sessions(): SyncSessionProvider = object : SyncSessionProvider {
        override fun activeSession(): SyncSession? = null
    }
}
