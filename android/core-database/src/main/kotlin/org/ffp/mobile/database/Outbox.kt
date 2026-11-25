package org.ffp.mobile.database

import android.content.Context
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.Room
import androidx.room.RoomDatabase
import kotlinx.coroutines.flow.Flow
import net.zetetic.database.sqlcipher.SupportOpenHelperFactory
import org.ffp.mobile.security.KeystoreVault
import java.util.UUID

enum class TransactionKind {
    PURCHASE_REQUISITION, PAYMENT_REQUEST, DELIVERY_ORDER, RETURN_TO_STOCK, INVENTORY_COUNT,
}

enum class TransactionStage {
    LOCALLY_SAVED, SERVER_RECEIVED, ACCEPTED, APPROVED, POSTED, CONFLICT, REJECTED;

    fun permits(next: TransactionStage): Boolean = when (this) {
        CONFLICT, REJECTED, POSTED -> next == this
        else -> next != LOCALLY_SAVED && next.ordinal >= ordinal
    }
}

@Entity(tableName = "outbox")
data class OutboxEntry(
    @PrimaryKey val id: String,
    val accountId: String,
    val applicationId: String,
    val kind: TransactionKind,
    val payload: String,
    val createdAtMillis: Long,
    val stage: TransactionStage = TransactionStage.LOCALLY_SAVED,
    val serverRevision: Long = 0,
    val issue: String? = null,
) {
    companion object {
        fun warehouse(accountId: String, kind: TransactionKind, payload: String): OutboxEntry {
            require(accountId.isNotBlank() && payload.isNotBlank())
            return OutboxEntry(
                UUID.randomUUID().toString(), accountId, "warehouse-operations",
                kind, payload, System.currentTimeMillis(),
            )
        }
    }
}

@Dao
interface OutboxDao {
    @Insert
    suspend fun insert(entry: OutboxEntry)

    @Query("""
        SELECT * FROM outbox WHERE applicationId = :applicationId AND accountId = :accountId
        ORDER BY createdAtMillis, id
    """)
    fun observe(applicationId: String, accountId: String): Flow<List<OutboxEntry>>

    @Query("""
        SELECT * FROM outbox WHERE applicationId = :applicationId AND accountId = :accountId
        AND stage IN ('LOCALLY_SAVED', 'SERVER_RECEIVED', 'ACCEPTED', 'APPROVED')
        AND (createdAtMillis > :afterTime OR (createdAtMillis = :afterTime AND id > :afterId))
        ORDER BY createdAtMillis, id LIMIT :limit
    """)
    suspend fun pending(
        applicationId: String, accountId: String, afterTime: Long, afterId: String, limit: Int,
    ): List<OutboxEntry>

    @Query("""
        UPDATE outbox SET stage = :stage, serverRevision = :revision, issue = :issue
        WHERE id = :id AND serverRevision = :expectedRevision
    """)
    suspend fun reconcile(
        id: String, expectedRevision: Long, stage: TransactionStage, revision: Long, issue: String?,
    ): Int
}

@Database(entities = [OutboxEntry::class], version = 1, exportSchema = true)
abstract class MobileDatabase : RoomDatabase() {
    abstract fun outbox(): OutboxDao

    companion object {
        fun open(context: Context, vault: KeystoreVault): MobileDatabase {
            System.loadLibrary("sqlcipher")
            return Room.databaseBuilder(context, MobileDatabase::class.java, "ffp-outbox.db")
                .openHelperFactory(SupportOpenHelperFactory(vault.databasePassphrase()))
                .build()
        }
    }
}
